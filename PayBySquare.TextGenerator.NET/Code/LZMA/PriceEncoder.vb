
Namespace LZMA

    Friend Class PriceEncoder

        Public Const SymbolsTotal As Integer = LowSymbols + MidSymbols + HighSymbols

        Private Const LowBits As Integer = 3
        Private Const LowSymbols As UInteger = 1 << LowBits
        Private Const MidBits As Integer = 3
        Private Const MidSymbols As UInteger = 1 << MidBits
        Private Const HighBits As Integer = 8
        Private Const HighSymbols As UInteger = 1 << HighBits

        Private fChoice1 As UShort = ProbInitValue
        Private fChoice2 As UShort = ProbInitValue
        Private fLow() As UShort = Enumerable.Repeat(ProbInitValue, PbStatesMax << LowBits).ToArray
        Private fMid() As UShort = Enumerable.Repeat(ProbInitValue, PbStatesMax << MidBits).ToArray
        Private fHigh() As UShort = Enumerable.Repeat(ProbInitValue, HighSymbols).ToArray
        Private fPrices(PbStatesMax - 1)() As UInteger

        Public Sub New(ProbPrices() As UInteger, PosStates As Integer)
            Dim SymbolCnt As Integer = FastBytes + 1 - Lzma1Encoder.MatchLenMin
            For i As Integer = 0 To fPrices.Length - 1
                fPrices(i) = New UInteger(SymbolsTotal - 1) {}
            Next
            For PosState As Integer = 0 To PosStates - 1
                SetPrices(PosState, SymbolCnt, fPrices(PosState), ProbPrices)
            Next
        End Sub

        Public Sub Encode(Range As RangeEncoder, Symbol As UInteger, posState As Integer)
            If Symbol < LowSymbols Then
                Range.EncodeBit(fChoice1, False)
                Range.TreeEncode(fLow, posState << LowBits, LowBits, Symbol)
            Else
                Range.EncodeBit(fChoice1, True)
                If Symbol < LowSymbols + MidSymbols Then
                    Range.EncodeBit(fChoice2, False)
                    Range.TreeEncode(fMid, posState << MidBits, MidBits, Symbol - LowSymbols)
                Else
                    Range.EncodeBit(fChoice2, True)
                    Range.TreeEncode(fHigh, 0, HighBits, Symbol - LowSymbols - MidSymbols)
                End If
            End If
        End Sub

        Private Sub SetPrices(PosState As Integer, SymbolCnt As Integer, Prices() As UInteger, ProbPrices() As UInteger)
            Dim a0, a1, b0, b1 As UInteger
            a0 = ProbPrices(fChoice1 >> MoveReducingBits)
            a1 = ProbPrices((fChoice1 Xor (BitModelTotal - 1)) >> MoveReducingBits)
            b0 = a1 + ProbPrices(fChoice2 >> MoveReducingBits)
            b1 = a1 + ProbPrices((fChoice2 Xor (BitModelTotal - 1)) >> MoveReducingBits)
            For i As Integer = 0 To SymbolCnt - 1
                If i < LowSymbols Then
                    Prices(i) = a0 + TreeGetPrice(fLow, PosState << LowBits, LowBits, CUInt(i), ProbPrices)
                ElseIf i < LowSymbols + MidSymbols Then
                    Prices(i) = b0 + TreeGetPrice(fMid, PosState << MidBits, MidBits, CUInt(i) - LowSymbols, ProbPrices)
                Else
                    Prices(i) = b1 + TreeGetPrice(fHigh, 0, HighBits, CUInt(i) - (LowSymbols + MidSymbols), ProbPrices)
                End If
            Next
        End Sub

        Private Function TreeGetPrice(Probs() As UShort, ProbsPos As Integer, BitLevels As Integer, Symbol As UInteger, ProbPrices() As UInteger) As UInteger
            Dim Ret As UInteger
            Symbol = Symbol Or (1UI << BitLevels)
            While Symbol <> 1
                Ret += GetPrice(ProbPrices, Probs(ProbsPos + CInt(Symbol >> 1)), (Symbol And 1UI) <> 0)
                Symbol >>= 1
            End While
            Return Ret
        End Function

        Private Function GetPrice(ProbPrices() As UInteger, Prob As UShort, Symbol As Boolean) As UInteger
            Return ProbPrices((Prob Xor (If(Symbol, Integer.MaxValue, 0) And (BitModelTotal - 1))) >> MoveReducingBits)
        End Function

    End Class

End Namespace
