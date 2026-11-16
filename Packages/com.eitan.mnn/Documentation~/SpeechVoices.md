# 语音合成音色选择

使用 `MNNSpeechSynthesizer.GetVoiceOptions(modelDirectory)` 从当前导出的本地元数据和音色资源读取选项，
不加载原生模型、不请求网络，也不维护一份与模型脱节的音色表。已通过 `MNNModelRegistry`
注册的模型使用其注册的音色读取器；其他已实现的导出沿用各自读取器。

```csharp
var choices = MNNSpeechSynthesizer.GetVoiceOptions(modelDirectory);
string[] ids = choices.VoiceIds; // 绑定到应用的下拉框
// 空字符串/null 表示模型默认；选择内置音色时原样传递其 ID。
var options = new MNNSpeechSynthesisOptions { Voice = selectedId };
using var speech = MNNSpeechSynthesizer.Load(modelDirectory, options);
```

`ModelKind == Unknown` 时没有可选音色。先完成模型安装，再重新查询。
`VoiceIds` 是独立数组；查询结果是快照，增删音色或切换模型后重新获取。
查询成功仅证明读取了音色信息，不代表模型资源完整、平台受支持或推理通过。
读取失败或格式损坏会抛出对应异常，应用应提示验证安装，不应猜测音色。

`RequiresReferenceAudio` 为 `true` 时，`VoiceIds` 为空，声音来自用户选择的参考录音，
使用 `ReferenceWavePath` 以及模型要求的 `ReferenceText` 配置。
“Reference audio” 是说明文字，不是可以写入 `Voice` 的 ID。
固定单音色导出仍返回其既有标识，例如 CosyVoice 的 `Default`。
`SupportsVoice` 可以验证现有设置，`ResolveVoice` 校验后返回原 ID（模型默认返回 `null`）。
显式 ID 按大小写精确匹配；无效 ID 在统一合成器和会话语音提供者加载原生模型之前拒绝，
抛出 `ArgumentException`，其 `ParamName` 为 `Voice`。

## Unity 组件 Inspector

开发者自己的组件可以复用相同选项：

```csharp
public string modelDirectory;
[MNNSpeechVoice(nameof(modelDirectory))]
public string voice;
```

两个字段必须为同一序列化对象中的字符串字段。Inspector 只提供模型默认和检测到的音色，
选择后保存原始 ID；运行时仍通过 `MNNSpeechSynthesizer.Load` 校验。
无效的已有 ID 在 Inspector 显示为未选择，用户可选择模型默认或当前音色。
自定义 Inspector 可调用 `MNN.Unity.Editor.MNNSpeechVoiceField.Draw`，无需重复读取模型元数据。

## SiriAI

本地 MNN 语音设置及 Configurable Backend Inspector 使用同一查询 API。
运行时 Voice 为选择列表，没有自定义文本输入。切换模型清空旧音色；重新打开设置时，
已不在当前模型列表中的旧 ID 会提示并恢复模型默认。
未安装或元数据损坏时不提供猜测选项，也不清除暂时不可读取的已保存值。
参考录音模型继续显示其 WAV/转写配置。云端服务沿用各服务自身的音色配置协议。

仓库忽略本地 SiriAI 导入；可审查的改动保存于 `Tools~/Migration/SiriAISpeechVoices.patch`。
精确匹配该补丁基线的导入可以执行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage speech-voices
```

离线回归覆盖注册模型、固定音色、文件音色、参考录音、未知导出、损坏元数据、
快照隔离、无效 ID 的加载前拒绝和 SiriAI 实际选项交互。这些测试不作为原生推理证明。
