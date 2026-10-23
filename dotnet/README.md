# kaswallet (.NET / C#)

基于 [IgraLabs/kaswallet](https://github.com/IgraLabs/kaswallet) 的 **C# / .NET 8** 改造与演进。仓库名保持原开源项目名 **kaswallet**（fork：[`composehk/kaswallet`](https://github.com/composehk/kaswallet)）。

> **状态**：Schnorr 收款地址生成（x-only + Kaspa bech32 校验和）已完成。交易签名与广播仍是实验骨架：BIP-340 消息签名可以往返校验，但 Kaspa 共识 sighash 尚未实现，`POST /transactions` 只提交占位草稿。请勿用于主网真实资金。

## 结构

```
dotnet/   # 本 C# 解决方案（推送到 GitHub 后位于仓库的 dotnet/ 目录）
  KasWallet.sln
  src/KasWallet.Core   # 地址、BIP44 派生、REST、未签名交易草稿
  src/KasWallet.Cli    # 命令行（程序集名 kaswallet）
  tests/...
```

上游 Rust 实现仍保留在仓库根目录，供对照。

## 构建

```bash
cd dotnet
dotnet restore
dotnet build
dotnet test
```

## CLI 示例

```bash
dotnet run --project src/KasWallet.Cli -- networks
dotnet run --project src/KasWallet.Cli -- validate 'kaspatest:q...'
dotnet run --project src/KasWallet.Cli -- new-mnemonic --words 12
dotnet run --project src/KasWallet.Cli -- address --index 0
# mnemonic from --mnemonic or env KASWALLET_MNEMONIC; private keys are not printed
dotnet run --project src/KasWallet.Cli -- balance 'kaspa:q...'
```

`new-mnemonic` 把助记词打到 stdout，把派生路径和 index 0 收款地址打到 stderr。

收款路径：`m/44'/111111'/0'/0/{index}`。找零路径：`m/44'/111111'/0'/1/{index}`。

## 广播

`KaspaRestClient.SubmitTransactionAsync` 向 `{baseUrl}/transactions` 提交 JSON（默认 `https://api.kaspa.org/transactions`，请求体对齐公开 OpenAPI 的 `SubmitTransactionRequest`）。当前草稿的 `signatureScript` 为空，节点会拒绝。不要把该调用当成主网发送。

## 路线图

- [x] 网络与地址格式校验（含 bech32 校验和与版本长度）
- [x] 公共 REST：余额 / UTXO
- [x] BIP39 助记词与 BIP44 `m/44'/111111'/0'/0/{index}` Schnorr 地址
- [x] 未签名支付草稿（UTXO 选择、派生找零地址、可 JSON 序列化）
- [ ] Kaspa 共识 sighash 签名（消息级 BIP-340 已有）
- [ ] 主网安全的广播（实验性 `POST /transactions` 已留接口）
- [ ] gRPC（对齐上游 daemon）
- [ ] keys.json 兼容与批量发送

## 许可

ISC — 见 `LICENSE` 与 `NOTICE.md`。
