namespace KasWallet.Core.Consensus;

public sealed class TransactionOutpoint
{
    public TransactionOutpoint(byte[] transactionId, uint index)
    {
        if (transactionId is null || transactionId.Length != 32)
            throw new ArgumentException("Transaction id must be 32 bytes.", nameof(transactionId));
        TransactionId = transactionId;
        Index = index;
    }

    public byte[] TransactionId { get; }
    public uint Index { get; }
}

public sealed class TransactionInput
{
    public TransactionInput(TransactionOutpoint previousOutpoint, byte[]? signatureScript, ulong sequence, byte sigOpCount)
    {
        PreviousOutpoint = previousOutpoint ?? throw new ArgumentNullException(nameof(previousOutpoint));
        SignatureScript = signatureScript ?? Array.Empty<byte>();
        Sequence = sequence;
        SigOpCount = sigOpCount;
    }

    public TransactionOutpoint PreviousOutpoint { get; }
    public byte[] SignatureScript { get; set; }
    public ulong Sequence { get; }
    public byte SigOpCount { get; }
}

public sealed class ScriptPublicKey
{
    public ScriptPublicKey(ushort version, byte[] script)
    {
        Version = version;
        Script = script ?? throw new ArgumentNullException(nameof(script));
    }

    public ushort Version { get; }
    public byte[] Script { get; }
}

public sealed class TransactionOutput
{
    public TransactionOutput(ulong value, ScriptPublicKey scriptPublicKey)
    {
        Value = value;
        ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
    }

    public ulong Value { get; set; }
    public ScriptPublicKey ScriptPublicKey { get; }
}

/// <summary>
/// Consensus UTXO. Amount and script are what sighash commits to.
/// </summary>
public sealed class UtxoEntry
{
    public UtxoEntry(ulong amount, ScriptPublicKey scriptPublicKey, ulong blockDaaScore, bool isCoinbase)
    {
        Amount = amount;
        ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
        BlockDaaScore = blockDaaScore;
        IsCoinbase = isCoinbase;
    }

    public ulong Amount { get; }
    public ScriptPublicKey ScriptPublicKey { get; }
    public ulong BlockDaaScore { get; }
    public bool IsCoinbase { get; }
}

public sealed class Transaction
{
    public Transaction(
        ushort version,
        IEnumerable<TransactionInput> inputs,
        IEnumerable<TransactionOutput> outputs,
        ulong lockTime,
        byte[] subnetworkId,
        ulong gas,
        byte[] payload)
    {
        if (subnetworkId is null || subnetworkId.Length != 20)
            throw new ArgumentException("Subnetwork id must be 20 bytes.", nameof(subnetworkId));
        Version = version;
        Inputs = (inputs ?? throw new ArgumentNullException(nameof(inputs))).ToList();
        Outputs = (outputs ?? throw new ArgumentNullException(nameof(outputs))).ToList();
        LockTime = lockTime;
        SubnetworkId = subnetworkId;
        Gas = gas;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public ushort Version { get; }
    public List<TransactionInput> Inputs { get; }
    public List<TransactionOutput> Outputs { get; }
    public ulong LockTime { get; }
    public byte[] SubnetworkId { get; }
    public ulong Gas { get; }
    public byte[] Payload { get; }
}
