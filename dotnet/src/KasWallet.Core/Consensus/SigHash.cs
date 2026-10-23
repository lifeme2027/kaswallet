namespace KasWallet.Core.Consensus;

/// <summary>
/// Kaspa consensus sighash and transaction id.
/// Both use BLAKE2b-256 keyed with the domain string (rusty-kaspa sighash.rs).
/// SigHashAll only.
/// </summary>
public static class SigHash
{
    public const byte SigHashAll = 0x01;

    private static readonly byte[] SigningKey = "TransactionSigningHash"u8.ToArray();
    private static readonly byte[] TransactionIdKey = "TransactionID"u8.ToArray();

    public static byte[] CalcSchnorrSignatureHash(Transaction tx, int inputIndex, UtxoEntry utxo)
    {
        if (tx is null)
            throw new ArgumentNullException(nameof(tx));
        if (utxo is null)
            throw new ArgumentNullException(nameof(utxo));
        if ((uint)inputIndex >= (uint)tx.Inputs.Count)
            throw new ArgumentOutOfRangeException(nameof(inputIndex));

        var input = tx.Inputs[inputIndex];
        var preimage = new Writer();
        preimage.U16(tx.Version);
        preimage.Raw(PreviousOutputsHash(tx));
        preimage.Raw(SequencesHash(tx));
        preimage.Raw(SigOpCountsHash(tx));
        preimage.Outpoint(input.PreviousOutpoint);
        preimage.ScriptPublicKey(utxo.ScriptPublicKey);
        preimage.U64(utxo.Amount);
        preimage.U64(input.Sequence);
        preimage.U8(input.SigOpCount);
        preimage.Raw(OutputsHash(tx));
        preimage.U64(tx.LockTime);
        preimage.Raw(tx.SubnetworkId);
        preimage.U64(tx.Gas);
        preimage.Raw(PayloadHash(tx));
        preimage.U8(SigHashAll);
        return Blake2b.Hash256(preimage.AsSpan(), SigningKey);
    }

    /// <summary>
    /// Transaction id. Signature scripts are hashed as empty and sigop counts are omitted.
    /// </summary>
    public static byte[] TransactionId(Transaction tx)
    {
        if (tx is null)
            throw new ArgumentNullException(nameof(tx));

        var body = new Writer();
        body.U16(tx.Version);
        body.U64((ulong)tx.Inputs.Count);
        foreach (var input in tx.Inputs)
        {
            body.Outpoint(input.PreviousOutpoint);
            body.U64(0);
            body.U64(input.Sequence);
        }

        body.U64((ulong)tx.Outputs.Count);
        foreach (var output in tx.Outputs)
            body.Output(output);

        body.U64(tx.LockTime);
        body.Raw(tx.SubnetworkId);
        body.U64(tx.Gas);
        body.VarBytes(tx.Payload);
        return Blake2b.Hash256(body.AsSpan(), TransactionIdKey);
    }

    private static byte[] PreviousOutputsHash(Transaction tx)
    {
        var buf = new Writer();
        foreach (var input in tx.Inputs)
            buf.Outpoint(input.PreviousOutpoint);
        return Blake2b.Hash256(buf.AsSpan(), SigningKey);
    }

    private static byte[] SequencesHash(Transaction tx)
    {
        var buf = new Writer();
        foreach (var input in tx.Inputs)
            buf.U64(input.Sequence);
        return Blake2b.Hash256(buf.AsSpan(), SigningKey);
    }

    private static byte[] SigOpCountsHash(Transaction tx)
    {
        var buf = new Writer();
        foreach (var input in tx.Inputs)
            buf.U8(input.SigOpCount);
        return Blake2b.Hash256(buf.AsSpan(), SigningKey);
    }

    private static byte[] OutputsHash(Transaction tx)
    {
        var buf = new Writer();
        foreach (var output in tx.Outputs)
            buf.Output(output);
        return Blake2b.Hash256(buf.AsSpan(), SigningKey);
    }

    private static byte[] PayloadHash(Transaction tx)
    {
        // Native subnetwork with an empty payload commits to the all-zero hash, not hash("").
        if (IsNative(tx.SubnetworkId) && tx.Payload.Length == 0)
            return new byte[32];

        var buf = new Writer();
        buf.VarBytes(tx.Payload);
        return Blake2b.Hash256(buf.AsSpan(), SigningKey);
    }

    private static bool IsNative(byte[] subnetworkId)
    {
        foreach (var b in subnetworkId)
        {
            if (b != 0)
                return false;
        }
        return true;
    }

    private sealed class Writer
    {
        private byte[] _buffer = new byte[256];
        private int _length;

        public void U8(byte value) => Ensure(1)[ _length++] = value;

        public void U16(ushort value)
        {
            var span = Ensure(2);
            span[_length++] = (byte)value;
            span[_length++] = (byte)(value >> 8);
        }

        public void U32(uint value)
        {
            var span = Ensure(4);
            span[_length++] = (byte)value;
            span[_length++] = (byte)(value >> 8);
            span[_length++] = (byte)(value >> 16);
            span[_length++] = (byte)(value >> 24);
        }

        public void U64(ulong value)
        {
            var span = Ensure(8);
            for (var i = 0; i < 8; i++)
                span[_length++] = (byte)(value >> (8 * i));
        }

        public void Raw(ReadOnlySpan<byte> data)
        {
            data.CopyTo(Ensure(data.Length).AsSpan(_length));
            _length += data.Length;
        }

        public void VarBytes(ReadOnlySpan<byte> data)
        {
            U64((ulong)data.Length);
            Raw(data);
        }

        public void Outpoint(TransactionOutpoint outpoint)
        {
            Raw(outpoint.TransactionId);
            U32(outpoint.Index);
        }

        public void ScriptPublicKey(ScriptPublicKey script)
        {
            U16(script.Version);
            VarBytes(script.Script);
        }

        public void Output(TransactionOutput output)
        {
            U64(output.Value);
            ScriptPublicKey(output.ScriptPublicKey);
        }

        public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, _length);

        private byte[] Ensure(int extra)
        {
            var needed = _length + extra;
            if (needed > _buffer.Length)
            {
                var grown = new byte[Math.Max(needed, _buffer.Length * 2)];
                _buffer.AsSpan(0, _length).CopyTo(grown);
                _buffer = grown;
            }
            return _buffer;
        }
    }
}
