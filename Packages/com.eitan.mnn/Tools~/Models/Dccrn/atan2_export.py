"""Export atan2 numeric quadrants and zero axes without a 0/0 slope."""
import math
import torch


def symbolic(g, y, x):
    def constant(value):
        return g.op('Constant', value_t=torch.tensor(value, dtype=torch.float32))
    zero, one = constant(0.), constant(1.)
    xzero, yzero = g.op('Equal', x, zero), g.op('Equal', y, zero)
    # Reciprocal carries the sign of zero. Ratios are evaluated only with a safe denominator.
    xnegative = g.op('Or', g.op('Less', x, zero), g.op('And', xzero, g.op('Less', g.op('Div', one, x), zero)))
    ynegative = g.op('Or', g.op('Less', y, zero), g.op('And', yzero, g.op('Less', g.op('Div', one, y), zero)))
    atan = g.op('Atan', g.op('Div', y, g.op('Where', xzero, one, x)))
    signed_pi = g.op('Where', ynegative, constant(-math.pi), constant(math.pi))
    quadrant = g.op('Where', xnegative, g.op('Add', atan, signed_pi), atan)
    origin = g.op('Where', xnegative, signed_pi, y)
    vertical = g.op('Where', ynegative, constant(-math.pi / 2), constant(math.pi / 2))
    return g.op('Where', yzero, origin, g.op('Where', xzero, vertical, quadrant))


def register():
    torch.onnx.register_custom_op_symbolic('aten::atan2', symbolic, 17)
