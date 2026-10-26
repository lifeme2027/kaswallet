# kaswallet (.NET / C#)

[![dotnet](https://github.com/composehk/kaswallet/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/composehk/kaswallet/actions/workflows/dotnet.yml)

基于 [IgraLabs/kaswallet](https://github.com/IgraLabs/kaswallet) 的 **C# / .NET 8** 改造与演进。仓库名保持原开源项目名 **kaswallet**（fork：[`composehk/kaswallet`](https://github.com/composehk/kaswallet)）。

> **状态**：Schnorr 收款地址、共识 sighash 与 BIP-340 签名、REST 提交、`send` 命令（默认 dry-run，`--broadcast` 才会提交）均已实现。新增交易质量（mass）计算（compute / transient / KIP-9 storage mass，移植自 rusty-kaspa `consensus/core/src/mass`，测试复现了上游官方向量）与按质量估算手续费：`fee = mass × fee-rate`，最低 100 sompi/gram。目前仅支持 version-0 交易与 Schnorr P2PK。仍是 pre-alpha，请勿用于主网真实资金。

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

`KaspaRestClient.SubmitTransactionAsync` 向 `{baseUrl}/transactions` 提交 JSON（默认 `https://api.kaspa.org/transactions`，请求体对齐公开 OpenAPI 的 `SubmitTransactionRequest`）。`build-tx` 草稿的 `signatureScript` 为空，节点会拒绝；`send` 生成的是已签名交易。不要把该调用当成主网发送。

## 发送与手续费

```bash
# dry-run：打印已签名交易 JSON（stdout），mass / fee 摘要在 stderr
dotnet run --project src/KasWallet.Cli -- send --from-key <hex或助记词> --to 'kaspatest:q...' \
  --amount 500000000 --api https://api-tn10.kaspa.org/ [--fee-rate 100] [--fee <sompi>] [--broadcast]
```

- `--fee-rate`：sompi/gram，默认且不得低于 100（网络最低）。手续费 = 归一化最大质量 × 费率。
- 质量取 compute、transient（归一化后为 2/字节）、storage（KIP-9，C = 10^12）三者的最大值；小额输出或过小的找零会使 storage mass 很大，手续费随之升高，超过标准上限 100000 时拒绝构建。
- `--fee`：显式固定手续费（sompi），覆盖估算；若低于 mass × 费率会在输出中给出 warning。
- 输出（stderr）：`mass`、`mass_compute`、`mass_transient`、`mass_storage`、`fee_rate`、`fee_sompi`、`fee_kas`、`change_sompi`。

## 路线图

- [x] 网络与地址格式校验（含 bech32 校验和与版本长度）
- [x] 公共 REST：余额 / UTXO
- [x] BIP39 助记词与 BIP44 `m/44'/111111'/0'/0/{index}` Schnorr 地址
- [x] 未签名支付草稿（UTXO 选择、派生找零地址、可 JSON 序列化）
- [x] Kaspa 共识 sighash 签名与 BIP-340 输入签名
- [x] 交易质量（mass）计算与按质量估算手续费（`send --fee-rate`）
- [ ] 主网安全的广播（`send --broadcast` 已可用于 testnet-10，主网尚未验证）
- [ ] gRPC（对齐上游 daemon）
- [ ] keys.json 兼容与批量发送

## 许可

ISC — 见 `LICENSE` 与 `NOTICE.md`。

## CI

本 fork 只启用 `.github/workflows/dotnet.yml`（GitHub 托管 ubuntu-latest，.NET 8 build + test）。上游 Rust/Docker 工作流依赖 IgraLabs 的自托管 runner（`[self-hosted, kaswallet]`），在 fork 上会一直排队，因此已在本仓 Actions 设置里停用（`gh workflow disable`），文件本身保持与上游一致，便于同步。
