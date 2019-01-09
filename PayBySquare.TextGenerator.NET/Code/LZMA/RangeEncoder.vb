
Namespace LZMA

    Friend Class RangeEncoder

        Private Const TopValue As UInteger = 1UI << 24

        Private fOutput As List(Of Byte)
        Private fBuff(1 << 16 - 1) As Byte
        Private fPos As Integer
        Private fRange As UInteger = UInteger.MaxValue
        Private fCache As Byte
        Private fLow, fCacheSize As ULong

        Public Sub New(Output As List(Of Byte))
            fOutput = Output
            fCacheSize = 1
        End Sub

        Public Sub FlushStream()
            fOutput.AddRange(fBuff.Take(fPos))
            fPos = 0
        End Sub

        Public Sub FlushData()
            For i As Integer = 0 To 4
                ShiftLow()
            Next
        End Sub

        Private Sub ShiftLow()
            If (fLow And UInteger.MaxValue) < &HFF000000UI OrElse (fLow >> 32) <> 0 Then
                Dim B As Byte = fCache
                While fCacheSize <> 0
                    fBuff(fPos) = CByte((B + (fLow >> 32) And &HFFUL) And &HFFUL)
                    fPos += 1
                    If fPos = fBuff.Length Then FlushStream()
                    B = &HFF
                    fCacheSize -= 1UL
                End While
                fCache = CByte((fLow And UInteger.MaxValue) >> 24)
            End If
            fCacheSize += 1UL
            fLow = (fLow << 8) And UInteger.MaxValue
        End Sub

        Public Sub EncodeDirectBits(value As UInteger, BitCnt As Integer)
            While BitCnt <> 0
                fRange >>= 1
                BitCnt -= 1
                If (value And (1 << BitCnt)) <> 0 Then fLow += fRange
                If fRange < TopValue Then
                    fRange <<= 8
                    ShiftLow()
                End If
            End While
        End Sub

        Public Sub EncodeBit(ByRef Prob As UShort, Value As Boolean)
            Dim NewBound As UInteger = (fRange >> BitModelTotalBitCnt) * Prob
            If Value Then
                fLow += NewBound
                fRange -= NewBound
                Prob -= Prob >> MoveBitCnt
            Else
                fRange = NewBound
                Prob += CUShort(BitModelTotal - Prob) >> MoveBitCnt
            End If
            If fRange < TopValue Then
                fRange <<= 8
                ShiftLow()
            End If
        End Sub

        Public Sub TreeEncode(Probs() As UShort, ProbsPos As Integer, BitCnt As Integer, Symbol As UInteger)
            Dim m As Integer = 1, Bit As Boolean
            For i As Integer = BitCnt - 1 To 0 Step -1
                Bit = ((Symbol >> i) And 1UI) <> 0
                EncodeBit(Probs(ProbsPos + m), Bit)
                m = (m << 1) + If(Bit, 1, 0)
            Next
        End Sub

        Public Sub TreeReverseEncode(Probs() As UShort, ProbsPos As Integer, BitCnt As Integer, Symbol As UInteger)
            Dim m As Integer = 1, Bit As Boolean
            For i As Integer = 0 To BitCnt - 1
                Bit = ((Symbol >> i) And 1UI) <> 0
                EncodeBit(Probs(ProbsPos + m), Bit)
                m = (m << 1) + If(Bit, 1, 0)
            Next
        End Sub

        Public Sub Encode(Probs() As UShort, ProbsPos As Integer, Symbol As UInteger)
            Symbol = Symbol Or &H100UI
            Do
                EncodeBit(Probs(CInt(ProbsPos + (Symbol >> 8))), ((Symbol >> 7) And 1) = 1)
                Symbol <<= 1
            Loop While Symbol < &H10000UI
        End Sub

        Public Sub EncodeMatched(Probs() As UShort, ProbsPos As Integer, Symbol As UInteger, MatchByte As UInteger)
            Dim Offset As UInteger = &H100UI
            Symbol = Symbol Or &H100UI
            Do
                MatchByte <<= 1
                EncodeBit(Probs(CInt(ProbsPos + (Offset + (MatchByte And Offset) + (Symbol >> 8)))), ((Symbol >> 7) And 1) = 1)
                Symbol <<= 1
                Offset = Offset And Not (MatchByte Xor Symbol)
            Loop While Symbol < &H10000UI
        End Sub

    End Class

End Namespace
