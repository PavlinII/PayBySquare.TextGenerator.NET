
Namespace LZMA

    'Lzma1Encoder is based on C# managed-lzma project.
    'https://github.com/weltkante/managed-lzma

    'Do NOT rewrite this LZMA namespace back to C#, that would be waste of time.
    'Link to managed-lzma project directly (start with ManagedLzma.LZMA.AsyncEncoder) or use it's content to replace local code structure.
    'Original code was cleaned to remain only LZMA1-related stuff. Constants, members and method's names were slightly renamed.

    Public Class Lzma1Encoder

#Region "Constants"

        Friend Const MatchLenMin As UInteger = 2

        Private Const MatchLenMax As Integer = MatchLenMin + PriceEncoder.SymbolsTotal - 1
        Private Const PriceShiftBits As Integer = 4
        Private Const LogBits As UInteger = 9 + 4 '9 + sizeof(Of Long) / 2
        Private Const DicLogSizeMaxCompress As Integer = ((LogBits - 1) * 2 + 7)
        Private Const LzmaNumReps As UInteger = 4
        Private Const OptCnt As Integer = (1 << 12)
        Private Const LenToPosStates As Integer = 4
        Private Const PosSlotBits As Integer = 6
        Private Const AlignBits As Integer = 4
        Private Const AlignTableSize As Integer = (1 << AlignBits)
        Private Const AlignMask As UInteger = (AlignTableSize - 1)
        Private Const StartPosModelIndex As Integer = 4
        Private Const EndPosModelIndex As Integer = 14
        Private Const FullDistances As Integer = (1 << (EndPosModelIndex >> 1))
        Private Const StatesCnt As Integer = 12

        Private ReadOnly fLiteralNextStates() As Integer = {0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 4, 5}
        Private ReadOnly fMatchNextStates() As Integer = {7, 7, 7, 7, 7, 7, 7, 10, 10, 10, 10, 10}
        Private ReadOnly fRepNextStates() As Integer = {8, 8, 8, 8, 8, 8, 8, 11, 11, 11, 11, 11}
        Private ReadOnly fShortRepNextStates() As Integer = {9, 9, 9, 9, 9, 9, 9, 11, 11, 11, 11, 11}

#End Region

#Region "Promenne"

        Private fMatchFinder As MatchFinder
        Private fRange As RangeEncoder
        Private fLenEnc As PriceEncoder
        Private fRepLenEnc As PriceEncoder
        Private fReps As OptimumReps

        Private fPairCnt, fState As Integer
        Private fLongestMatchLength, fAdditionalOffset, fLpMask, fPbMask As UInteger
        Private fNowPos64 As ULong
        Private fFastPos((1 << LogBits) - 1) As Byte
        Private fProbPrices((BitModelTotal >> MoveReducingBits) - 1) As UInteger
        Private fMatches(MatchLenMax * 2 + 2 + 1 - 1) As UInteger
        Private fLitProbs() As UShort
        Private fIsRep(StatesCnt - 1) As UShort
        Private fIsRepG0(StatesCnt - 1) As UShort
        Private fIsRepG1(StatesCnt - 1) As UShort
        Private fIsRepG2(StatesCnt - 1) As UShort
        Private fPosAlignEncoder((1 << AlignBits) - 1) As UShort
        Private fPosEncoders(FullDistances - EndPosModelIndex - 1) As UShort

        Private fIsMatch As UShort()() = Init(Of UShort)(StatesCnt, PB_STATES_MAX)
        Private fIsRep0Long As UShort()() = Init(Of UShort)(StatesCnt, PB_STATES_MAX)
        Private fPosSlotEncoder As UShort()() = Init(Of UShort)(LenToPosStates, 1 << PosSlotBits)

#End Region

        Public Sub New()
            InitFastPos()
            InitProbPrices()
        End Sub

#Region "Init"

        Private Function Init(Of ItemType)(Size1 As Integer, Size2 As Integer) As ItemType()()
            Dim Ret(Size2 - 1)() As ItemType
            For i As Integer = 0 To Ret.Length - 1
                Ret(i) = New ItemType(Size2 - 1) {}
            Next
            Return Ret
        End Function

        Private Sub InitFastPos()
            Dim si, i, j, k As Integer
            fFastPos(0) = 0
            fFastPos(1) = 1
            i = 2
            For si = 2 To LogBits * 2 - 1
                k = 1 << ((si >> 1) - 1)
                For j = 0 To k - 1
                    fFastPos(i) = CByte(si)
                    i += 1
                Next
            Next
        End Sub

        Private Sub InitProbPrices()
            Dim w, bitCnt, j As UInteger
            For i As Integer = 0 To fProbPrices.Length - 1
                w = 8UI + CUInt(i) * (1UI << MoveReducingBits) : bitCnt = 0
                For j = 0 To PriceShiftBits - 1
                    w = w * w
                    bitCnt <<= 1
                    While w >= 1UI << 16
                        w >>= 1
                        bitCnt += 1UI
                    End While
                Next
                fProbPrices(i) = (BitModelTotalBitCnt << PriceShiftBits) - 15UI - bitCnt
            Next
        End Sub

#End Region

#Region "Hlavni"

        Public Function Encode(InputData() As Byte) As Byte()
            Dim Ret As New List(Of Byte)
            fRange = New RangeEncoder(Ret)
            fMatchFinder = New MatchFinder(InputData, OptCnt, FastBytes, MatchLenMax)
            fLenEnc = New PriceEncoder(fProbPrices, 1 << PB)
            fRepLenEnc = New PriceEncoder(fProbPrices, 1 << PB)
            fReps = New OptimumReps()
            ResetState()
            While CodeOneBlock()
                'Nic
            End While
            Return Ret.ToArray()
        End Function

        Private Sub ResetState()
            Dim i, j As Integer
            fState = 0
            fAdditionalOffset = 0
            fNowPos64 = 0
            fLitProbs = Enumerable.Repeat(Of UShort)(ProbInitValue, &H300 << (LC + LP)).ToArray
            For i = 0 To StatesCnt - 1
                For j = 0 To PB_STATES_MAX - 1
                    fIsMatch(i)(j) = ProbInitValue
                    fIsRep0Long(i)(j) = ProbInitValue
                Next
                fIsRep(i) = ProbInitValue
                fIsRepG0(i) = ProbInitValue
                fIsRepG1(i) = ProbInitValue
                fIsRepG2(i) = ProbInitValue
            Next
            For i = 0 To LenToPosStates - 1
                For j = 0 To (1 << PosSlotBits) - 1
                    fPosSlotEncoder(i)(j) = ProbInitValue
                Next
            Next
            For i = 0 To FullDistances - EndPosModelIndex - 1
                fPosEncoders(i) = ProbInitValue
            Next
            For i = 0 To (1 << AlignBits) - 1
                fPosAlignEncoder(i) = ProbInitValue
            Next
            fPbMask = (1UI << PB) - 1
            fLpMask = (1UI << LP) - 1
        End Sub

        Private Function CodeOneBlock() As Boolean
            Dim NowPos32, Startpos32, Len, Pos, Distance, PosSlot, Base, PosReduced As UInteger, PosState, BufferPos, ProbsPos, FooterBits As Integer
            NowPos32 = CUInt(fNowPos64 And UInteger.MaxValue)
            Startpos32 = NowPos32
            If fNowPos64 = 0 Then
                If fMatchFinder.AvailableBytes() = 0 Then
                    Flush()
                    Return False
                End If
                ReadMatchDistances(0)
                fRange.EncodeBit(fIsMatch(fState)(0), False)
                fState = fLiteralNextStates(fState)
                fRange.Encode(fLitProbs, 0, fMatchFinder.Buffer(fMatchFinder.BufferPos - CInt(fAdditionalOffset)))
                fAdditionalOffset -= 1UI
                NowPos32 += 1UI
            End If
            If fMatchFinder.AvailableBytes() <> 0 Then
                Do
                    Pos = 0
                    Len = GetOptimumFast(Pos)
                    PosState = CInt(NowPos32 And fPbMask)
                    If Len = 1 AndAlso Pos = UInteger.MaxValue Then
                        fRange.EncodeBit(fIsMatch(fState)(PosState), False)
                        BufferPos = fMatchFinder.BufferPos - CInt(fAdditionalOffset)
                        ProbsPos = CInt((((NowPos32 And fLpMask) << LC) + (CUInt(fMatchFinder.Buffer(BufferPos - 1)) >> (8 - LC))) * &H300UI)
                        If fState < 7 Then 'IsCharState
                            fRange.Encode(fLitProbs, ProbsPos, fMatchFinder.Buffer(BufferPos))
                        Else
                            fRange.EncodeMatched(fLitProbs, ProbsPos, fMatchFinder.Buffer(BufferPos), fMatchFinder.Buffer(BufferPos - CInt(fReps.x0) - 1))
                        End If
                        fState = fLiteralNextStates(fState)
                    Else
                        fRange.EncodeBit(fIsMatch(fState)(PosState), True)
                        If Pos < LzmaNumReps Then
                            fRange.EncodeBit(fIsRep(fState), True)
                            If Pos = 0 Then
                                fRange.EncodeBit(fIsRepG0(fState), False)
                                fRange.EncodeBit(fIsRep0Long(fState)(PosState), Len <> 1)
                            Else
                                Distance = fReps(Pos)
                                fRange.EncodeBit(fIsRepG0(fState), True)
                                If Pos = 1 Then
                                    fRange.EncodeBit(fIsRepG1(fState), False)
                                Else
                                    fRange.EncodeBit(fIsRepG1(fState), True)
                                    fRange.EncodeBit(fIsRepG2(fState), Pos = 3)
                                    If Pos = 3 Then fReps.x3 = fReps.x2
                                    fReps.x2 = fReps.x1
                                End If
                                fReps.x1 = fReps.x0
                                fReps.x0 = Distance
                            End If

                            If Len = 1 Then
                                fState = fShortRepNextStates(fState)
                            Else
                                fRepLenEnc.Encode(fRange, Len - MatchLenMin, PosState)
                                fState = fRepNextStates(fState)
                            End If
                        Else
                            fRange.EncodeBit(fIsRep(fState), False)
                            fState = fMatchNextStates(fState)
                            fLenEnc.Encode(fRange, Len - MatchLenMin, PosState)
                            Pos -= LzmaNumReps
                            PosSlot = GetPosSlot(Pos)
                            fRange.TreeEncode(fPosSlotEncoder(LenToPosState(CInt(Len))), 0, PosSlotBits, PosSlot)

                            If PosSlot >= StartPosModelIndex Then
                                FooterBits = CInt((PosSlot >> 1) - 1)
                                Base = (2UI Or (PosSlot And 1UI)) << FooterBits
                                PosReduced = Pos - Base
                                If PosSlot < EndPosModelIndex Then
                                    fRange.TreeReverseEncode(fPosEncoders, CInt(Base - PosSlot - 1), FooterBits, PosReduced)
                                Else
                                    fRange.EncodeDirectBits(PosReduced >> AlignBits, (FooterBits - AlignBits))
                                    fRange.TreeReverseEncode(fPosAlignEncoder, 0, AlignBits, PosReduced And AlignMask)
                                End If
                            End If
                            fReps.x3 = fReps.x2
                            fReps.x2 = fReps.x1
                            fReps.x1 = fReps.x0
                            fReps.x0 = Pos
                        End If
                    End If
                    fAdditionalOffset -= Len
                    NowPos32 += Len
                    If fAdditionalOffset = 0 Then
                        If fMatchFinder.AvailableBytes() = 0 Then Exit Do
                        If (NowPos32 - Startpos32 >= (1 << 15)) Then    'If Processed>xxx Then
                            fNowPos64 += NowPos32 - Startpos32
                            Return True
                        End If
                    End If
                Loop
            End If
            fNowPos64 += NowPos32 - Startpos32
            Flush()
            Return False
        End Function


#End Region

#Region "Pomocne"

        Private Function LenToPosState(Len As Integer) As Integer
            Return If(Len < LenToPosStates + 1, Len - 2, LenToPosStates - 1)
        End Function

        Private Function ChangePair(SmallDist As UInteger, BigDist As UInteger) As Boolean
            Return (BigDist >> 7) > SmallDist
        End Function

        Private Function GetPosSlot(Pos As UInteger) As UInteger
            If Pos < FullDistances Then
                Return fFastPos(CInt(Pos))
            Else
                Dim i As UInteger = 6UI + ((LogBits - 1UI) And (0UI - ((((1UI << (LogBits + 6)) - 1UI) - Pos) >> 31)))
                Return fFastPos(CInt(Pos >> CInt(i))) + (i * 2UI)
            End If
        End Function

        Private Sub Flush()
            fRange.FlushData()
            fRange.FlushStream()
        End Sub

        Private Sub MovePos(Cnt As UInteger)
            If Cnt <> 0 Then
                fAdditionalOffset += Cnt
                fMatchFinder.Hc4Skip(Cnt)
            End If
        End Sub

        Private Function ReadMatchDistances(ByRef RetPairs As Integer) As UInteger
            Dim AvailCnt, PBy As Integer, Ret, Distance As UInteger
            AvailCnt = If(fMatchFinder.AvailableBytes() > MatchLenMax, MatchLenMax, CInt(fMatchFinder.AvailableBytes()))
            RetPairs = fMatchFinder.Hc4FindMatches(fMatches)
            If RetPairs > 0 Then
                Ret = fMatches(RetPairs - 2)
                If Ret = FastBytes Then
                    PBy = fMatchFinder.BufferPos - 1
                    Distance = fMatches(RetPairs - 1) + 1UI
                    While (Ret < AvailCnt AndAlso fMatchFinder.Buffer(CInt(PBy + Ret)) = fMatchFinder.Buffer(CInt(PBy - Distance + Ret)))
                        Ret += 1UI
                    End While
                End If
            End If
            fAdditionalOffset += 1UI
            Return Ret
        End Function

        Private Function GetOptimumFast(ByRef RetBack As UInteger) As UInteger
            Dim Ret, Avail, MainDist As UInteger, PairCnt, BufferPos, i, Len, RepLen, RepIndex As Integer
            If fAdditionalOffset = 0 Then
                Ret = ReadMatchDistances(PairCnt)
            Else
                Ret = fLongestMatchLength
                PairCnt = fPairCnt
            End If
            Avail = fMatchFinder.AvailableBytes()
            RetBack = UInteger.MaxValue
            If Avail < 2 Then Return 1
            If Avail > MatchLenMax Then Avail = MatchLenMax
            BufferPos = fMatchFinder.BufferPos - 1
            For i = 0 To LzmaNumReps - 1
                If fMatchFinder.Buffer(BufferPos) <> fMatchFinder.Buffer(BufferPos - CInt(fReps(i)) - 1) OrElse fMatchFinder.Buffer(BufferPos + 1) <> fMatchFinder.Buffer(BufferPos - CInt(fReps(i))) Then
                    Continue For
                End If
                Len = 2
                While Len < Avail AndAlso fMatchFinder.Buffer(BufferPos + Len) = fMatchFinder.Buffer(BufferPos - CInt(fReps(i)) - 1 + Len)
                    Len += 1
                End While
                If Len >= FastBytes Then
                    RetBack = CUInt(i)
                    MovePos(CUInt(Len - 1))
                    Return CUInt(Len)
                End If
                If Len > RepLen Then
                    RepIndex = i
                    RepLen = Len
                End If
            Next
            If Ret >= FastBytes Then
                RetBack = fMatches(PairCnt - 1) + LzmaNumReps
                MovePos(Ret - 1UI)
                Return Ret
            End If
            If Ret >= 2 Then
                MainDist = fMatches(PairCnt - 1)
                While PairCnt > 2 AndAlso Ret = fMatches(PairCnt - 4) + 1
                    If Not ChangePair(fMatches(PairCnt - 3), MainDist) Then Exit While
                    PairCnt -= 2
                    Ret = fMatches(PairCnt - 2)
                    MainDist = fMatches(PairCnt - 1)
                End While
                If Ret = 2 AndAlso MainDist >= &H80UI Then Ret = 1
            End If
            If RepLen >= 2 AndAlso ((RepLen + 1 >= Ret) OrElse (RepLen + 2 >= Ret AndAlso MainDist >= (1UI << 9)) OrElse (RepLen + 3 >= Ret AndAlso MainDist >= (1UI << 15))) Then
                RetBack = CUInt(RepIndex)
                MovePos(CUInt(RepLen - 1))
                Return CUInt(RepLen)
            End If
            If Ret < 2 OrElse Avail <= 2 Then Return 1
            fLongestMatchLength = ReadMatchDistances(fPairCnt)
            If fLongestMatchLength >= 2 Then
                If (fLongestMatchLength >= Ret AndAlso fMatches(fPairCnt - 1) < MainDist) OrElse
                            (fLongestMatchLength = Ret + 1 AndAlso Not ChangePair(MainDist, fMatches(fPairCnt - 1))) OrElse
                            (fLongestMatchLength > Ret + 1) OrElse
                            (fLongestMatchLength + 1 >= Ret AndAlso Ret >= 3 AndAlso ChangePair(fMatches(fPairCnt - 1), MainDist)) Then
                    Return 1
                End If
            End If
            BufferPos = fMatchFinder.BufferPos - 1
            For i = 0 To LzmaNumReps - 1
                If fMatchFinder.Buffer(BufferPos) <> fMatchFinder.Buffer(BufferPos - CInt(fReps(i)) - 1) OrElse fMatchFinder.Buffer(BufferPos + 1) <> fMatchFinder.Buffer(BufferPos - CInt(fReps(i))) Then
                    Continue For
                End If
                Len = 2
                While Len < Ret - 1 AndAlso fMatchFinder.Buffer(BufferPos + Len) = fMatchFinder.Buffer(BufferPos - CInt(fReps(i)) - 1 + Len)
                    Len += 1
                End While
                If Len >= Ret - 1 Then Return 1
            Next
            RetBack = MainDist + LzmaNumReps
            MovePos(Ret - 2UI)
            Return Ret
        End Function

#End Region

    End Class

End Namespace
