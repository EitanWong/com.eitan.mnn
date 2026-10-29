"""CPU tests for teacher labels and masking, independent of large checkpoints."""
import unittest
import torch
import torch.nn.functional as F
from objectives import cfg_logits, masked_kl, grouped_schedule, advance


class ObjectiveTests(unittest.TestCase):
    def test_cfg_matches_upstream_log_probability_guidance(self):
        torch.manual_seed(7)
        c, u = torch.randn(1, 8, 3, 1025), torch.randn(1, 8, 3, 1025)
        expected = F.log_softmax(3 * F.log_softmax(c, -1) - 2 * F.log_softmax(u, -1), -1)
        self.assertTrue(torch.allclose(F.log_softmax(cfg_logits(c, u), -1), expected, atol=3e-6))

    def test_loss_ignores_already_committed_tokens_and_backpropagates(self):
        teacher = torch.zeros(1, 8, 2, 1025)
        student = teacher.clone().requires_grad_()
        with torch.no_grad(): student[:, :, 0, 3] = 4
        mask = torch.zeros(1, 8, 2, dtype=torch.bool)
        mask[:, :, 1] = True
        loss = masked_kl(student, teacher, mask)
        self.assertAlmostEqual(loss.item(), 0, places=6)
        loss.backward()
        self.assertEqual(student.grad[:, :, 0].abs().sum().item(), 0)
        self.assertTrue(torch.isfinite(student.grad).all())
        changed = teacher.clone(); changed[:, :, 1, 4] = 5
        self.assertGreater(masked_kl(student, changed, mask).item(), 0.1)

    def test_grouping_preserves_ceil_mask_budget(self):
        fine = grouped_schedule(400, 32, 32)
        coarse = grouped_schedule(400, 32, 16)
        self.assertEqual(sum(fine), 400)
        self.assertEqual(coarse, [sum(fine[i:i+2]) for i in range(0,32,2)])
        with self.assertRaises(ValueError): grouped_schedule(400, 32, 12)

    def test_mask_class_is_in_confidence_but_never_selected(self):
        values = torch.zeros(1, 8, 2, 1025)
        values[..., 1024] = 20; values[..., 17] = 10
        tokens = torch.full((1,8,2), 1024, dtype=torch.long)
        tokens[0,0,0] = 2
        updated = advance(tokens, values, 4, torch.Generator().manual_seed(4))
        self.assertEqual(updated[0,0,0].item(), 2)
        self.assertEqual((updated != 1024).sum().item(), 5)
        self.assertTrue(torch.all(updated[updated != 1024][1:] < 1024))
        # Only two candidates remain; a dominant mask class must make the first
        # less confident even though both positions predict code 17.
        tokens.fill_(2); tokens[0,0,:]=1024
        values[...,1024]=-100; values[0,0,0,1024]=1000
        chosen=advance(tokens,values,1,torch.Generator().manual_seed(4))
        self.assertEqual(chosen[0,0,0].item(),1024)
        self.assertEqual(chosen[0,0,1].item(),17)


if __name__ == '__main__': unittest.main()
