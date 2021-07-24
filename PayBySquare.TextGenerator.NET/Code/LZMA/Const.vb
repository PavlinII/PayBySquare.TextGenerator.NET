
Namespace LZMA

    Friend Module Consts

        'Parametry
        Public Const DictSize As UInteger = 131072
        Public Const MC As UInteger = 16
        Public Const LC As Integer = 3
        Public Const LP As Integer = 0
        Public Const PB As Integer = 2
        Public Const FastBytes As Integer = 32

        'LZMA
        Public Const BitModelTotalBitCnt As UInteger = 11
        Public Const BitModelTotal As Integer = 1 << BitModelTotalBitCnt
        Public Const MoveBitCnt As Integer = 5
        Public Const PbStatesMax As Integer = 1 << 4  'LZMA_PB_MAX = 4
        Public Const ProbInitValue As UShort = BitModelTotal >> 1
        Public Const MoveReducingBits As Integer = 4

    End Module

End Namespace
