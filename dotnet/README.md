# kaswallet (.NET / C#)

基于 [IgraLabs/kaswallet](https://github.com/IgraLabs/kaswallet) 的 **C# / .NET 8** 改造与演进。仓库名保持原开源项目名 **kaswallet**（fork：[`composehk/kaswallet`](https://github.com/composehk/kaswallet)）。

> **警告（pre-alpha）**：请勿用于主网真实资金。Schnorr 签名、完整地址编码与广播尚未完成。

## 结构

```
dotnet/   # 本 C# 解决方案（推送到 GitHub 后位于仓库的 dotnet/ 目录）
  KasWallet.sln
  src/KasWallet.Core   # 地址校验、REST、助记词、未签名交易草稿
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
dotnet run --project src/KasWallet.Cli -- validate-address 'kaspatest:q...'
dotnet run --project src/KasWallet.Cli -- balance 'kaspa:q...'
dotnet run --project src/KasWallet.Cli -- new-mnemonic --words 12
```

## 路线图

- [x] 网络与地址格式校验
- [x] 公共 REST：余额 / UTXO
- [x] BIP39 助记词与 BIP44 coin type 111111 说明
- [x] 未签名支付草稿（UTXO 选择）
- [ ] Kaspa Schnorr 地址生成与签名
- [ ] gRPC（对齐上游 daemon）与广播
- [ ] keys.json 兼容与批量发送

## 许可

ISC — 见 `LICENSE` 与 `NOTICE.md`。
