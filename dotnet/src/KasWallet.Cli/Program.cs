using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using KasWallet;
using KasWallet.Cli;
using KasWallet.Core.Consensus;

static void Warn()
{
    Console.Error.WriteLine("kaswallet (C#) — pre-alpha. Inspired by IgraLabs/kaswallet. Do not use with real funds.");
}

static KaspaNetwork ParseNetwork(string name)
{
    if (!KaspaNetworks.TryParse(name, out var network))
        throw new ArgumentException("Unknown network. Use mainnet, testnet-10, devnet, or simnet.");
    return network;
}

static string? ResolveMnemonic(string? argument)
{
    if (!string.IsNullOrWhiteSpace(argument))
        return argument.Trim();
    var fromEnv = Environment.GetEnvironmentVariable("KASWALLET_MNEMONIC");
    return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
}

static string? ResolvePassphrase(string? argument)
    => argument ?? Environment.GetEnvironmentVariable("KASWALLET_PASSPHRASE");

static void ValidateAddress(string address)
{
    var status = AddressValidator.Check(address);
    var ok = status == AddressCheck.Valid;
    Console.WriteLine(ok ? "valid" : "invalid");
    Console.WriteLine(status switch
    {
        AddressCheck.Valid => "checksum=ok",
        AddressCheck.BadChecksum => "checksum=bad",
        AddressCheck.BadPayload => "checksum=ok",
        _ => "checksum=n/a"
    });
    if (status == AddressCheck.BadPayload)
        Console.WriteLine("payload=bad");
    if (ok && AddressValidator.TryDecode(address, out var info) && AddressValidator.TryDetectNetwork(address, out var net))
    {
        Console.WriteLine($"network={KaspaNetworks.DisplayName(net)}");
        Console.WriteLine($"version={info.Version}");
        Console.WriteLine($"payload_bytes={info.Payload.Length}");
    }

    Environment.ExitCode = ok ? 0 : 1;
}

var root = new RootCommand("kaswallet — Kaspa wallet toolkit (.NET / C# port)");

var versionCmd = new Command("version", "Show version");
versionCmd.SetHandler(() =>
{
    Warn();
    Console.WriteLine("kaswallet-cli 0.2.0-alpha (.NET 8)");
});
root.AddCommand(versionCmd);

var networksCmd = new Command("networks", "List supported networks");
networksCmd.SetHandler(() =>
{
    foreach (var n in KaspaNetworks.All)
        Console.WriteLine($"{KaspaNetworks.DisplayName(n)}\tprefix={KaspaNetworks.AddressPrefix(n)}:");
});
root.AddCommand(networksCmd);

var addrArg = new Argument<string>("address", "Kaspa address");
var validateCmd = new Command("validate", "Validate a Kaspa address, including the bech32 checksum");
validateCmd.AddArgument(addrArg);
validateCmd.SetHandler(ValidateAddress, addrArg);
root.AddCommand(validateCmd);

var validateAlias = new Command("validate-address", "Validate a Kaspa address, including the bech32 checksum");
var addrArgAlias = new Argument<string>("address", "Kaspa address");
validateAlias.AddArgument(addrArgAlias);
validateAlias.SetHandler(ValidateAddress, addrArgAlias);
root.AddCommand(validateAlias);

var balanceCmd = new Command("balance", "Query address balance via public REST API");
var balanceAddress = new Argument<string>("address", "Kaspa address");
balanceCmd.AddArgument(balanceAddress);
var baseUrlOpt = new Option<string?>("--api", "REST base URL (default https://api.kaspa.org/)");
balanceCmd.AddOption(baseUrlOpt);
balanceCmd.SetHandler(async (string address, string? api) =>
{
    Warn();
    using var client = new KaspaRestClient(api);
    var bal = await client.GetBalanceAsync(address);
    Console.WriteLine($"address={bal.Address}");
    Console.WriteLine($"balance_sompi={bal.BalanceSompi}");
    Console.WriteLine($"balance_kas={bal.BalanceKas:F8}");
}, balanceAddress, baseUrlOpt);
root.AddCommand(balanceCmd);

var utxosCmd = new Command("utxos", "List UTXOs for an address");
var utxoAddress = new Argument<string>("address", "Kaspa address");
utxosCmd.AddArgument(utxoAddress);
var utxoApiOpt = new Option<string?>("--api", "REST base URL (default https://api.kaspa.org/)");
utxosCmd.AddOption(utxoApiOpt);
utxosCmd.SetHandler(async (string address, string? api) =>
{
    Warn();
    using var client = new KaspaRestClient(api);
    var utxos = await client.GetUtxosAsync(address);
    Console.WriteLine(JsonSerializer.Serialize(utxos, new JsonSerializerOptions { WriteIndented = true }));
}, utxoAddress, utxoApiOpt);
root.AddCommand(utxosCmd);

var wordsOpt = new Option<int>("--words", () => 12, "12 or 24");
var mnemonicNetworkOpt = new Option<string>("--network", () => "mainnet", "mainnet, testnet-10, devnet, or simnet");
var mnemonicCmd = new Command("new-mnemonic", "Generate a BIP39 mnemonic and its first receive address");
mnemonicCmd.AddOption(wordsOpt);
mnemonicCmd.AddOption(mnemonicNetworkOpt);
mnemonicCmd.SetHandler((int words, string networkName) =>
{
    Warn();
    try
    {
        var network = ParseNetwork(networkName);
        var phrase = MnemonicService.Generate(words);
        var address = SchnorrAddress.GenerateReceiveAddress(phrase, 0, network);
        Console.Error.WriteLine($"derivation={KeyDerivation.ReceivePath(0)}");
        Console.Error.WriteLine($"network={KaspaNetworks.DisplayName(network)}");
        Console.Error.WriteLine($"receive_address={address}");
        Console.Error.WriteLine("Store the mnemonic offline. Private keys are not printed.");
        Console.WriteLine(phrase);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}, wordsOpt, mnemonicNetworkOpt);
root.AddCommand(mnemonicCmd);

var addressMnemonicOpt = new Option<string?>("--mnemonic", "BIP39 mnemonic (or set KASWALLET_MNEMONIC)");
var indexOpt = new Option<int>("--index", () => 0, "Receive address index");
var addressNetworkOpt = new Option<string>("--network", () => "mainnet", "mainnet, testnet-10, devnet, or simnet");
var passphraseOpt = new Option<string?>("--passphrase", "BIP39 passphrase (or set KASWALLET_PASSPHRASE). Not printed.");
var addressCmd = new Command("address", "Derive a Schnorr receive address. Does not print private keys.");
addressCmd.AddOption(addressMnemonicOpt);
addressCmd.AddOption(indexOpt);
addressCmd.AddOption(addressNetworkOpt);
addressCmd.AddOption(passphraseOpt);
addressCmd.SetHandler((string? mnemonicArg, int index, string networkName, string? passphraseArg) =>
{
    var mnemonic = ResolveMnemonic(mnemonicArg);
    if (mnemonic is null)
    {
        Console.Error.WriteLine("Missing mnemonic. Pass --mnemonic or set KASWALLET_MNEMONIC.");
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        var network = ParseNetwork(networkName);
        var address = SchnorrAddress.GenerateReceiveAddress(mnemonic, index, network, ResolvePassphrase(passphraseArg));
        Console.WriteLine($"network={KaspaNetworks.DisplayName(network)}");
        Console.WriteLine($"path={KeyDerivation.ReceivePath(index)}");
        Console.WriteLine(address);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}, addressMnemonicOpt, indexOpt, addressNetworkOpt, passphraseOpt);
root.AddCommand(addressCmd);

var deriveCmd = new Command("derive-info", "Show Kaspa BIP44 path notes");
deriveCmd.SetHandler(() =>
{
    Console.WriteLine($"coin_type={MnemonicService.KaspaCoinType}");
    Console.WriteLine($"account_path={MnemonicService.DefaultDerivationPath}");
    Console.WriteLine($"receive_path=m/44'/{MnemonicService.KaspaCoinType}'/0'/0/{{index}}");
    Console.WriteLine($"change_path=m/44'/{MnemonicService.KaspaCoinType}'/0'/1/{{index}}");
    Console.WriteLine("address_encoding=schnorr x-only bech32 (version 0)");
    Console.WriteLine("tx_signing=experimental (BIP-340 message signatures only; consensus sighash is not enabled)");
});
root.AddCommand(deriveCmd);

var fromOpt = new Option<string>("--from", "Funding address") { IsRequired = true };
var toOpt = new Option<string>("--to", "Destination address") { IsRequired = true };
var amountOpt = new Option<decimal>("--amount", "Amount in KAS") { IsRequired = true };
var feeOpt = new Option<decimal>("--fee", () => 0.0001m, "Fee in KAS");
var buildApiOpt = new Option<string?>("--api", "REST base URL (default https://api.kaspa.org/)");
var buildMnemonicOpt = new Option<string?>("--mnemonic", "When set, change goes to m/44'/111111'/0'/1/{change-index} (or KASWALLET_MNEMONIC)");
var changeIndexOpt = new Option<int>("--change-index", () => 0, "Change index used with --mnemonic");
var buildCmd = new Command("build-tx", "Dry-run payment draft from live UTXOs (signatures are placeholders)");
buildCmd.AddOption(fromOpt);
buildCmd.AddOption(toOpt);
buildCmd.AddOption(amountOpt);
buildCmd.AddOption(feeOpt);
buildCmd.AddOption(buildApiOpt);
buildCmd.AddOption(buildMnemonicOpt);
buildCmd.AddOption(changeIndexOpt);
buildCmd.SetHandler(async (string from, string to, decimal amount, decimal fee, string? api, string? mnemonicArg, int changeIndex) =>
{
    Warn();
    if (!AddressValidator.TryDetectNetwork(from, out var net))
        throw new InvalidOperationException("Invalid --from address");

    var mnemonic = ResolveMnemonic(mnemonicArg);
    string? change = from;
    string? changePath = null;
    if (mnemonic is not null)
    {
        change = SchnorrAddress.GenerateChangeAddress(mnemonic, changeIndex, net, ResolvePassphrase(null));
        changePath = KeyDerivation.ChangePath(changeIndex);
    }

    using var client = new KaspaRestClient(api);
    var utxos = await client.GetUtxosAsync(from);
    var draft = TransactionBuilder.BuildDraft(
        net,
        utxos,
        to,
        (long)(amount * 100_000_000m),
        change,
        changePath,
        (long)(fee * 100_000_000m));
    Console.WriteLine(JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }));
}, fromOpt, toOpt, amountOpt, feeOpt, buildApiOpt, buildMnemonicOpt, changeIndexOpt);
root.AddCommand(buildCmd);

var sendFromKey = new Option<string>("--from-key", "Hex private key (32 bytes), or a BIP39 mnemonic spent from receive index 0") { IsRequired = true };
var sendTo = new Option<string>("--to", "Destination address") { IsRequired = true };
var sendAmount = new Option<long>("--amount", "Amount in sompi") { IsRequired = true };
var sendFee = new Option<long?>("--fee", "Fixed fee in sompi (overrides --fee-rate; default: estimate from transaction mass)");
var sendFeeRate = new Option<ulong>("--fee-rate", () => MassCalculator.MinimumFeeRate, $"Fee rate in sompi per gram of mass (default and minimum {MassCalculator.MinimumFeeRate})");
var sendApi = new Option<string?>("--api", $"REST base URL (default {KaspaRestClient.DefaultBaseUrl}, testnet-10 {KaspaRestClient.Testnet10BaseUrl})");
var sendBroadcast = new Option<bool>("--broadcast", "POST the signed transaction. Omit to print JSON and not broadcast.");
var sendCmd = new Command("send", "Build a signed payment with a mass-based fee. Dry-run unless --broadcast is set.");
sendCmd.AddOption(sendFromKey);
sendCmd.AddOption(sendTo);
sendCmd.AddOption(sendAmount);
sendCmd.AddOption(sendFee);
sendCmd.AddOption(sendFeeRate);
sendCmd.AddOption(sendApi);
sendCmd.AddOption(sendBroadcast);
sendCmd.SetHandler(async (string fromKey, string to, long amount, long? fee, ulong feeRate, string? api, bool broadcast) =>
{
    Warn();
    Environment.ExitCode = await SendCommand.RunAsync(
        new SendOptions(fromKey, to, amount, fee, feeRate, broadcast, ResolvePassphrase(null)),
        api,
        Console.Out,
        Console.Error);
}, sendFromKey, sendTo, sendAmount, sendFee, sendFeeRate, sendApi, sendBroadcast);
root.AddCommand(sendCmd);

return await root.InvokeAsync(args);
