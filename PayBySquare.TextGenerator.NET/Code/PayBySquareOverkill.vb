
Public Class PayBySquareOverkill

    Public InvoiceID As String
    Public Payments As New List(Of Payment)

    Public Sub New()
    End Sub

    Public Sub New(P As Payment)
        Payments.Add(P)
    End Sub

    Public Sub New(IBAN As String, Amount As Decimal, CurrencyCode As String, VariableSymbol As String, PaymentNote As String)
        Me.New(New Payment(IBAN, Amount, CurrencyCode, VariableSymbol, PaymentNote))
    End Sub

    Public Function GeneratePayBySquareOverkillString() As String
        Dim TS As New TabSerializer, Crc As New Crc32, Enc As New LZMA.Lzma1Encoder, Value(), DataToCompress(), Buff() As Byte
        TS.Append(InvoiceID)
        TS.Append(Payments.Count)
        For Each P As Payment In Payments
            ValidatePayment(P)
            TS.Append(1)                'PaymentOptions= paymentorder=1, standingorder=2, directdebit=4
            TS.Append(P.Amount.ToString.Replace(",", "."))
            TS.Append(P.CurrencyCode)
            TS.Append(P.PaymentDueDate)
            TS.Append(P.VariableSymbol)
            TS.Append(P.ConstantSymbol)
            TS.Append(P.SpecificSymbol)
            TS.Append(P.PayerReference)
            TS.Append(If(P.PaymentNote IsNot Nothing AndAlso P.PaymentNote.Length > 140, P.PaymentNote.Substring(0, 140), P.PaymentNote))
            TS.Append(P.BankAccounts.Count)
            For Each BA As BankAccount In P.BankAccounts
                TS.Append(BA.IBAN)
                TS.Append(BA.BIC)
            Next
            TS.Append(0)                    'No StandingOrderExt structure, refer to XSD schema for implementation
            TS.Append(0)                    'No DirectDebitExt structure, refer to XSD schema for implementation
            TS.Append(P.BeneficiaryName)
            TS.Append(P.BeneficiaryAddressLine1)
            TS.Append(P.BeneficiaryAddressLine2)
        Next
        Value = Text.Encoding.UTF8.GetBytes(TS.ToString) 'TrimEnd tabs
        DataToCompress = Crc.ComputeHash(Value).Concat(Value).ToArray
        Buff = New Byte() {0, 0}.Concat(BitConverter.GetBytes(CShort(DataToCompress.Length))).Concat(Enc.Encode(DataToCompress)).ToArray    '4x4 bits (Type=0, Version=0, DocumentType=0, Reserved=0) & 16 bit little endian length of DataToCompress (crc & value) & LzmaCompressedData
        Return ToBase32Hex(Buff)
    End Function

    Private Sub ValidatePayment(Payment As Payment)
        If Not String.IsNullOrWhiteSpace(Payment.PayerReference) AndAlso
           Not (String.IsNullOrWhiteSpace(Payment.VariableSymbol) AndAlso
                String.IsNullOrWhiteSpace(Payment.ConstantSymbol) AndAlso
                String.IsNullOrWhiteSpace(Payment.SpecificSymbol)) Then
            Throw New InvalidOperationException("PayerReference cannot be used simultaneously with any of the variable, specific, or constant symbols. If PayerReference is used, the variable, specific, and constant symbols must all be empty.")
        End If
    End Sub

    Private Function ToBase32Hex(Buff() As Byte) As String
        Const Base32HexCharset As String = "0123456789ABCDEFGHIJKLMNOPQRSTUV"
        Dim CharIndex As Integer, sb As New Text.StringBuilder, B As Byte, bi, ByteIdx, BitIdx As Integer
        For BitPos As Integer = 0 To Buff.Length * 8 Step 5 'Cist jako bitstream po 5 bitech
            CharIndex = 0
            For bi = 0 To 4
                ByteIdx = (BitPos + bi) \ 8
                BitIdx = 7 - (BitPos + bi) Mod 8
                If ByteIdx = Buff.Length Then
                    B = 0   'Do nasobku 5 bitu se doplnuje nulami na konci
                Else
                    B = Buff(ByteIdx)
                End If
                If (B And (1 << BitIdx)) <> 0 Then CharIndex += 1 << (4 - bi)
            Next
            sb.Append(Base32HexCharset(CharIndex))
        Next
        Return sb.ToString
    End Function

End Class
