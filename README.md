# Nexus-AeroOS: 工业级低空物流全息自主中台

## 项目愿景
Nexus-AeroOS 是一个基于 **.NET 8** 和 **Semantic Kernel** 构建的自主智能体框架，旨在处理复杂物流场景下的实时遥测数据与应急决策。它通过感知机载 telemetry 数据，联动本地 RAG 向量记忆库，实现了一套“感知-裁决-行动”的全自动化工业级闭环控制系统。

## 核心架构亮点

- **感知与脱壳层**：利用 TelemetryParsingAgent 将非结构化语音/遥测数据转化为强类型的 C# Record 实体。
- **动态记忆中枢 (RAG)**：基于 PostgreSQL + pgvector + BAAI/bge-m3 向量模型，实现了实时规章制度检索。解决了非英语文本在默认 Tokenizer 下的切片边界问题。
- **自主行为闭环 (ReAct)**：智能体能够自主判断当前的动力失控与载荷风险，通过 ReAct 模式调用物理干涉插件（UavEmergencyPlugin），不仅是生成内容，更能产生物理影响。
- **网络鲁棒性设计**：设计了自定义 `DelegatingHandler` 来处理私有网络环境下的 SSL 握手拦截，确保在任何复杂网络环境下均能稳定接入硅基流动（SiliconFlow）等推理后端。

## 关键技术栈
- **核心框架**: Microsoft.SemanticKernel (SK)
- **向量存储**: PostgreSQL 16 + pgvector (HNSW 索引)
- **推理引擎**: DeepSeek-V3.2 (通过 SiliconFlow API)
- **向量模型**: BAAI/bge-m3
- **底层驱动**: Npgsql + Dapper (实现了 Vector 类型处理器)

## 工程挑战与解决方案
- **Q: 中文 Token 计数不准导致切片失效？**
  A: 实现了自定义 `TokenCounter` 委托，将字符长度映射为权重，强制语义分块边界对齐。
- **Q: 代理软件导致的 SSL 握手失败？**
  A: 构建了自定义 DelegatingHandler 并实现了证书验证回调（ServerCertificateCustomValidationCallback），实现了开发环境下的零摩擦连接。
- **Q: ORM 类型阻抗失配？**
  A: 编写 `DapperVectorTypeHandler`，使 Dapper 能够无缝解析 pgvector 格式的二进制流。

## 闭环演示
系统成功演示了：检测到电池超温 -> 检索公司章程 -> 生成干涉决策理由 -> 远程调用 WMS 物理继电器 -> 全网广播。

---
*该项目旨在探索 AI 在高危物流领域的极限应用，展示了从指令式编程到知识驱动自主决策的演进过程。*
