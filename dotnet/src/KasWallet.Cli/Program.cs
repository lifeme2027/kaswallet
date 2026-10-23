using System.CommandLine;
using System.Text.Json;
using KasWallet;

static void Warn()
{
    Console.Error.WriteLine("kaswallet (C#) — pre-alpha. Inspired by IgraLabs/kaswallet. Do not use with real funds.");
}

var root = new RootCommand("kaswallet — Kaspa wallet toolkit (.NET / C# port)");

var versionCmd = new Command("version", "Show version");
versionCmd.SetHandler(() =>
{
    Warn();
    Console.WriteLine("kaswallet-cli 0.1.0-alpha (.NET 8)");
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
var validateCmd = new Command("validate-address", "Validate a Kaspa address");
validateCmd.AddArgument(addrArg);
validateCmd.SetHandler((string address) =>
{
    var ok = AddressValidator.IsValid(address);
    Console.WriteLine(ok ? "valid" : "invalid");
    if (ok && AddressValidator.TryDetectNetwork(address, out var net))
        Console.WriteLine($"network={KaspaNetworks.DisplayName(net)}");
    Environment.ExitCode = ok ? 0 : 1;
}, addrArg);
root.AddCommand(validateCmd);

var balanceCmd = new Command("balance", "Query address balance via public REST API");
balanceCmd.AddArgument(addrArg);
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
}, addrArg, baseUrlOpt);
root.AddCommand(balanceCmd);

var utxosCmd = new Command("utxos", "List UTXOs for an address");
utxosCmd.AddArgument(addrArg);
utxosCmd.AddOption(baseUrlOpt);
utxosCmd.SetHandler(async (string address, string? api) =>
{
    Warn();
    using var client = new KaspaRestClient(api);
    var utxos = await client.GetUtxosAsync(address);
    Console.WriteLine(JsonSerializer.Serialize(utxos, new JsonSerializerOptions { WriteIndented = true }));
}, addrArg, baseUrlOpt);
root.AddCommand(utxosCmd);

var wordsOpt = new Option<int>("--words", () => 12, "12 or 24");
var mnemonicCmd = new Command("new-mnemonic", "Generate a BIP39 mnemonic (printed once; store offline)");
mnemonicCmd.AddOption(wordsOpt);
mnemonicCmd.SetHandler((int words) =>
{
    Warn();
    Console.Error.WriteLine($"derivation={MnemonicService.DefaultDerivationPath} (address encoding WIP)");
    Console.WriteLine(MnemonicService.Generate(words));
}, wordsOpt);
root.AddCommand(mnemonicCmd);

var deriveCmd = new Command("derive-info", "Show Kaspa BIP44 path notes");
deriveCmd.SetHandler(() =>
{
    Console.WriteLine($"coin_type={MnemonicService.KaspaCoinType}");
    Console.WriteLine($"account_path={MnemonicService.DefaultDerivationPath}");
    Console.WriteLine("Schnorr + Kaspa address encoding: not yet implemented in this C# port.");
});
root.AddCommand(deriveCmd);

var fromOpt = new Option<string>("--from", "Funding address") { IsRequired = true };
var toOpt = new Option<string>("--to", "Destination address") { IsRequired = true };
var amountOpt = new Option<decimal>("--amount", "Amount in KAS") { IsRequired = true };
var feeOpt = new Option<decimal>("--fee", () => 0.0001m, "Fee in KAS");
var buildCmd = new Command("build-tx", "Dry-run unsigned payment plan from live UTXOs");
buildCmd.AddOption(fromOpt);
buildCmd.AddOption(toOpt);
buildCmd.AddOption(amountOpt);
buildCmd.AddOption(feeOpt);
buildCmd.AddOption(baseUrlOpt);
buildCmd.SetHandler(async (string from, string to, decimal amount, decimal fee, string? api) =>
{
    Warn();
    if (!AddressValidator.TryDetectNetwork(from, out var net))
        throw new InvalidOperationException("Invalid --from address");
    using var client = new KaspaRestClient(api);
    var utxos = await client.GetUtxosAsync(from);
    var plan = TransactionBuilder.BuildPayment(
        net,
        utxos,
        to,
        (long)(amount * 100_000_000m),
        from,
        (long)(fee * 100_000_000m));
    Console.WriteLine(JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
}, fromOpt, toOpt, amountOpt, feeOpt, baseUrlOpt);
root.AddCommand(buildCmd);

return await root.InvokeAsync(args);
