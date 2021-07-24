
Namespace LZMA

    Friend Class OptimumReps

        Public x0, x1, x2, x3 As UInteger   'number of slots == LZMA_NUM_REPS

        Default Public Property Item(Index As Long) As UInteger
            Get
                Select Case Index
                    Case 0 : Return x0
                    Case 1 : Return x1
                    Case 2 : Return x2
                    Case 3 : Return x3
                    Case Else : Throw New InvalidOperationException
                End Select
            End Get
            Set(Value As UInteger)
                Select Case Index
                    Case 0 : x0 = Value
                    Case 1 : x1 = Value
                    Case 2 : x2 = Value
                    Case 3 : x3 = Value
                    Case Else : Throw New InvalidOperationException
                End Select
            End Set
        End Property

    End Class

End Namespace
