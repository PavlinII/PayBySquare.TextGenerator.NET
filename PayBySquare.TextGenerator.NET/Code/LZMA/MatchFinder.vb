
Namespace LZMA

    Friend Class MatchFinder

        Private Const Hash2Size As UInteger = 1 << 10
        Private Const Hash3Size As UInteger = 1 << 16
        Private Const Fix3HashSize As UInteger = Hash2Size
        Private Const Fix4HashSize As UInteger = Hash2Size + Hash3Size
        Private Const MaxValForNormalize As UInteger = UInteger.MaxValue
        Private Const NormalizeMask As UInteger = Not ((1UI << 10) - 1UI)
        Private Const CyclicBufferSize As UInteger = DictSize + 1

        Public ReadOnly Buffer() As Byte
        Public ReadOnly Property BufferPos As Integer

        Private fInputData() As Byte
        Private fBlockSize, fKeepSizeBefore, fKeepSizeAfter As UInteger

        Private fMatchMaxLen As UInteger
        Private fHashMask As UInteger
        Private fHash() As UInteger
        Private fHashSizeSum As UInteger
        Private fPos As UInteger
        Private fPosLimit As UInteger
        Private fStreamPos As UInteger
        Private fLenLimit As UInteger
        Private fCyclicBufferPos As UInteger
        Private fCrc As New Crc32

        Public Sub New(InputData() As Byte, KeepAddBufferBefore As UInteger, MatchMaxLen As UInteger, KeepAddBufferAfter As UInteger)
            Dim hs As UInteger
            fInputData = InputData
            fKeepSizeBefore = DictSize + KeepAddBufferBefore + 1UI
            fKeepSizeAfter = MatchMaxLen + KeepAddBufferAfter
            fBlockSize = fKeepSizeBefore + fKeepSizeAfter + (DictSize >> 1) + ((KeepAddBufferBefore + MatchMaxLen + KeepAddBufferAfter) >> 1) + (1UI << 19)
            Buffer = New Byte(CInt(fBlockSize - 1)) {}
            fMatchMaxLen = MatchMaxLen
            hs = DictSize - 1
            hs = hs Or (hs >> 1)
            hs = hs Or (hs >> 2)
            hs = hs Or (hs >> 4)
            hs = hs Or (hs >> 8)
            hs >>= 1
            hs = hs Or &HFFFFUI ' don't change it! It's required for Deflate
            If hs > 1UI << 24 Then hs >>= 1    'HashBytes=4
            fHashMask = hs
            fHashSizeSum = hs + 1UI + Hash2Size + Hash3Size
            fHash = New UInteger(CInt(fHashSizeSum + DictSize)) {}
            fPos = CyclicBufferSize
            fStreamPos = CyclicBufferSize
            ReadBlock()
            SetLimits()
        End Sub

        Private Sub ReadBlock()
            Dim Cnt, i As Integer
            If fInputData.Length = 0 Then Return
            Do
                If fBlockSize - (BufferPos + (fStreamPos - fPos)) = 0 Then Return
                Cnt = Math.Min(fInputData.Length, Buffer.Length)
                For i = 0 To Cnt - 1
                    Buffer(i) = fInputData(i)
                Next
                fInputData = fInputData.Skip(Cnt).ToArray
                If Cnt = 0 Then Return
                fStreamPos += CUInt(Cnt)
                If fStreamPos - fPos > fKeepSizeAfter Then Return
            Loop
        End Sub

        Private Function Min(A As UInteger, B As UInteger) As UInteger
            Return If(A < B, A, B)
        End Function

        Private Sub SetLimits()
            Dim Limit As UInteger = fStreamPos - fPos
            If Limit <= fKeepSizeAfter Then
                Limit = Min(Limit, 1)
            Else
                Limit -= fKeepSizeAfter
            End If
            fPosLimit = fPos + Min(Limit, Min(MaxValForNormalize - fPos, CyclicBufferSize - fCyclicBufferPos))
            fLenLimit = Min(fStreamPos - fPos, fMatchMaxLen)
        End Sub

        Private Sub TryMoveReadBlock()
            Dim Size, i As Integer, Src() As Byte
            If fBlockSize - BufferPos <= fKeepSizeAfter Then   'NeedMove
                Size = CInt(fStreamPos - fPos + fKeepSizeBefore)
                Src = Buffer.Skip(CInt(BufferPos - fKeepSizeBefore)).Take(Size).ToArray
                For i = 0 To Size - 1   'Shift
                    Buffer(i) = Src(i)
                Next
                _BufferPos = CInt(fKeepSizeBefore)
            End If
            ReadBlock()
        End Sub

        Private Sub Normalize()
            Dim subValue As UInteger
            subValue = (fPos - DictSize - 1UI) And NormalizeMask
            For i As Integer = 0 To CInt(fHashSizeSum + DictSize)
                If fHash(i) <= subValue Then
                    fHash(i) = 0    'Empty hash
                Else
                    fHash(i) -= subValue
                End If
            Next
            fPosLimit -= subValue 'Posunout offset
            fPos -= subValue
            fStreamPos -= subValue
        End Sub

        Private Sub CheckLimits()
            If fPos = MaxValForNormalize Then Normalize()
            If fInputData.Length <> 0 AndAlso fKeepSizeAfter = fStreamPos - fPos Then TryMoveReadBlock()
            If fCyclicBufferPos = CyclicBufferSize Then fCyclicBufferPos = 0
            SetLimits()
        End Sub

        Private Sub MovePos()
            fCyclicBufferPos += 1UI
            _BufferPos += 1
            fPos += 1UI
            If fPos = fPosLimit Then CheckLimits()
        End Sub

        Private Function FindMatches(CurMatch As UInteger, CutValue As UInteger, Distances() As UInteger, DistancesOffset As Integer, MaxLen As UInteger) As Integer
            Dim Delta, Len As UInteger
            fHash(CInt(fHashSizeSum + fCyclicBufferPos)) = CurMatch
            Do
                Delta = fPos - CurMatch
                If CutValue = 0 OrElse Delta >= CyclicBufferSize Then Return DistancesOffset
                CutValue -= 1UI
                CurMatch = fHash(CInt(fHashSizeSum + fCyclicBufferPos - Delta + If(Delta > fCyclicBufferPos, CyclicBufferSize, 0UI)))
                If Buffer(CInt(BufferPos - Delta + MaxLen)) = Buffer(CInt(BufferPos + MaxLen)) AndAlso Buffer(CInt(BufferPos - Delta)) = Buffer(BufferPos) Then
                    Len = 0
                    Do
                        Len += 1UI
                    Loop Until Len = fLenLimit OrElse Buffer(CInt(BufferPos - Delta + Len)) <> Buffer(CInt(BufferPos + Len))
                    If MaxLen < Len Then
                        MaxLen = Len
                        Distances(DistancesOffset) = Len
                        DistancesOffset += 1
                        Distances(DistancesOffset) = Delta - 1UI
                        DistancesOffset += 1
                        If Len = fLenLimit Then Return DistancesOffset
                    End If
                End If
            Loop
        End Function

        Public Function Hc4FindMatches(Distances() As UInteger) As Integer
            Dim Offset As Integer, Temp, Hash2, Hash3, Hash, Delta2, Delta3, CurMatch, MaxLen As UInteger
            If fLenLimit < 4 Then
                MovePos()
                Return 0
            End If
            Temp = fCrc.fTable(Buffer(BufferPos)) Xor Buffer(BufferPos + 1)
            Hash2 = Temp And (Hash2Size - 1UI)
            Hash3 = (Temp Xor (CUInt(Buffer(BufferPos + 2)) << 8)) And (Hash3Size - 1UI)
            Hash = (Temp Xor (CUInt(Buffer(BufferPos + 2)) << 8) Xor (fCrc.fTable(Buffer(BufferPos + 3)) << 5)) And fHashMask
            Delta2 = fPos - fHash(CInt(Hash2))
            Delta3 = fPos - fHash(CInt(Fix3HashSize + Hash3))
            CurMatch = fHash(CInt(Fix4HashSize + Hash))
            fHash(CInt(Hash2)) = fPos
            fHash(CInt(Fix3HashSize + Hash3)) = fPos
            fHash(CInt(Fix4HashSize + Hash)) = fPos
            MaxLen = 1
            If Delta2 < CyclicBufferSize AndAlso Buffer(CInt(BufferPos - Delta2)) = Buffer(BufferPos) Then
                MaxLen = 2
                Distances(0) = MaxLen
                Distances(1) = Delta2 - 1UI
                Offset = 2
            End If
            If Delta2 <> Delta3 AndAlso Delta3 < CyclicBufferSize AndAlso Buffer(CInt(BufferPos - Delta3)) = Buffer(BufferPos) Then
                MaxLen = 3
                Distances(Offset + 1) = Delta3 - 1UI
                Offset += 2
                Delta2 = Delta3
            End If
            If Offset <> 0 Then
                While MaxLen <> fLenLimit AndAlso Buffer(CInt(BufferPos + MaxLen - Delta2)) = Buffer(CInt(BufferPos + MaxLen))
                    MaxLen += 1UI
                End While
                Distances(Offset - 2) = MaxLen
                If MaxLen = fLenLimit Then
                    fHash(CInt(fHashSizeSum + fCyclicBufferPos)) = CurMatch
                    fCyclicBufferPos += 1UI
                    _BufferPos += 1
                    fPos += 1UI
                    If fPos = fPosLimit Then CheckLimits()
                    Return Offset
                End If
            End If
            If MaxLen < 3 Then MaxLen = 3
            Offset = FindMatches(CurMatch, MC, Distances, Offset, MaxLen)
            fCyclicBufferPos += 1UI
            _BufferPos += 1
            fPos += 1UI
            If fPos = fPosLimit Then CheckLimits()
            Return Offset
        End Function

        Public Sub Hc4Skip(Cnt As UInteger)
            Dim Temp, Hash2, Hash3, Hash, CurMatch As UInteger
            For i As UInteger = 1 To Cnt
                If fLenLimit < 4 Then
                    MovePos()
                Else
                    Temp = fCrc.fTable(Buffer(BufferPos)) Xor Buffer(BufferPos + 1)
                    Hash2 = Temp And (Hash2Size - 1UI)
                    Hash3 = (Temp Xor (CUInt(Buffer(BufferPos + 2)) << 8)) And (Hash3Size - 1UI)
                    Hash = (Temp Xor (CUInt(Buffer(BufferPos + 2)) << 8) Xor (fCrc.fTable(Buffer(BufferPos + 3)) << 5)) And fHashMask
                    CurMatch = fHash(CInt(Fix4HashSize + Hash))
                    fHash(CInt(Hash2)) = fPos
                    fHash(CInt(Fix3HashSize + Hash3)) = fPos
                    fHash(CInt(Fix4HashSize + Hash)) = fPos
                    fHash(CInt(fHashSizeSum + fCyclicBufferPos)) = CurMatch
                    fCyclicBufferPos += 1UI
                    _BufferPos += 1
                    fPos += 1UI
                    If fPos = fPosLimit Then CheckLimits()
                End If
            Next
        End Sub

        Public Function AvailableBytes() As UInteger
            Return fStreamPos - fPos
        End Function

    End Class

End Namespace
