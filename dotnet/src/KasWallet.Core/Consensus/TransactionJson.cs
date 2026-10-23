using System.Text.Json;

namespace KasWallet.Core.Consensus;

/// <summary>
/// JSON bodies for submitting a consensus <see cref="Transaction"/>.
/// Transaction ids are plain hex of the raw 32 bytes. Kaspa does not reverse
/// hash bytes the way Bitcoin does (<c>Hash::to_string</c> / <c>from_str</c>).
/// Script bytes, signatures, and the subnetwork id are the same straight hex.
/// u64 fields are JSON numbers.
/// </summary>
public static class TransactionJson
{
    public static string ToRestSubmitJson(Transaction transaction, bool allowOrphan = false)
    {
        if (transaction is null)
            throw new ArgumentNullException(nameof(transaction));

        return JsonSerializer.Serialize(new
        {
            transaction = new
            {
                version = transaction.Version,
                inputs = Inputs(transaction),
                outputs = Outputs(transaction),
                lockTime = transaction.LockTime,
                subnetworkId = Hex(transaction.SubnetworkId)
            },
            allowOrphan
        });
    }

    /// <summary>
    /// kaspad wRPC <c>submitTransaction</c> params. Same fields as the REST body,
    /// plus <c>gas</c> and <c>payload</c> on the transaction.
    /// </summary>
    public static string ToWrpcSubmitTransactionJson(Transaction transaction, bool allowOrphan = false)
    {
        if (transaction is null)
            throw new ArgumentNullException(nameof(transaction));

        return JsonSerializer.Serialize(new
        {
            transaction = new
            {
                version = transaction.Version,
                inputs = Inputs(transaction),
                outputs = Outputs(transaction),
                lockTime = transaction.LockTime,
                subnetworkId = Hex(transaction.SubnetworkId),
                gas = transaction.Gas,
                payload = Hex(transaction.Payload)
            },
            allowOrphan
        });
    }

    /// <summary>Hex of the raw 32-byte transaction id (no byte reversal).</summary>
    public static string FormatTransactionId(ReadOnlySpan<byte> consensusTransactionId)
    {
        if (consensusTransactionId.Length != 32)
            throw new ArgumentException("Transaction id must be 32 bytes.", nameof(consensusTransactionId));

        return Hex(consensusTransactionId);
    }

    /// <summary>Parse transaction-id hex into the raw 32 bytes (no byte reversal).</summary>
    public static byte[] ParseTransactionId(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Transaction id is required.", nameof(hex));

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(hex.Trim());
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Transaction id must be hex.", nameof(hex), ex);
        }

        if (bytes.Length != 32)
            throw new ArgumentException("Transaction id must be 32 bytes.", nameof(hex));

        return bytes;
    }

    private static object[] Inputs(Transaction transaction) => transaction.Inputs.Select(input => (object)new
    {
        previousOutpoint = new
        {
            transactionId = FormatTransactionId(input.PreviousOutpoint.TransactionId),
            index = input.PreviousOutpoint.Index
        },
        signatureScript = Hex(input.SignatureScript),
        sequence = input.Sequence,
        sigOpCount = input.SigOpCount
    }).ToArray();

    private static object[] Outputs(Transaction transaction) => transaction.Outputs.Select(output => (object)new
    {
        amount = output.Value,
        scriptPublicKey = new
        {
            version = output.ScriptPublicKey.Version,
            scriptPublicKey = Hex(output.ScriptPublicKey.Script)
        }
    }).ToArray();

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
