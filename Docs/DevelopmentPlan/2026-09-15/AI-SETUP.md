# 打包游戏的AI直连配置

用户已选择将开发者密钥编入游戏，使用直连，无需网关或玩家环境变量。本说明替代旧交接中的“必须部署网关”要求。

## 设置

编辑 `Assets/_Game/Scripts/Dialogue/EmbeddedAiConfig.cs`：

```csharp
public static readonly string ApiKey = "在本机填入实际密钥";
public const string Endpoint = "https://api.deepseek.com/chat/completions";
public const string Model = "deepseek-flash";
```

填入后重新编译/打包，游戏自动使用DeepSeek直连。仓库当前ApiKey为空，不含真实密钥；尚未进行真实付费调用。不要将示例文字当密钥。

若ApiKey为空，仍支持旧的可选网关环境变量；两者均没有时界面报告AI未配置并保留当前行程。修改代码中的密钥后，需要重新打包才能更新已发布客户端。

## 实现

- `DeepSeekDialogueService` 实现现有 `IDialogueService`，无需修改页面的调用方式。
- POST chat/completions，Bearer鉴权，stream=true，JSON输出模式，关闭思考模式，max_tokens=1024。
- 从模型生成的JSON中渐进提取text字段，处理跨片段转义/Unicode；只展示对白，不展示JSON元数据。
- 最终必须收到正常stop与[DONE]，完整解析结果后由Campaign再次验证话题、情绪、字数和提示；run/conversation/turn身份由本地请求补全，模型不能指定结算身份。
- 取消、超时、网络错误及截断响应沿用Coordinator恢复流程，不结算奖励。

模型ID依2026-09-15官方中文接口文档选`deepseek-flash`；原策划中的V4 Flash具体可用别名可能随服务调整，可在同一配置文件替换。

官方依据：https://api-docs.deepseek.com/zh-cn/api/create-chat-completion/

## 验收边界

离线测试使用内存HTTP传输替身，检查真实供应商协议的请求、流解析与失败处理；不是成功访问DeepSeek的证据。填入实际密钥后，还需六NPC实际对话、网络异常恢复及打包后联网验证。
