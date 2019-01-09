
Friend Class TabSerializer

    Private fSB As New Text.StringBuilder

    Public Sub Append(Value As Integer)
        Append(Value.ToString)
    End Sub

    Public Sub Append(Value? As Date)
        If Value.HasValue Then
            Append(Value.Value.ToString("yyyyMMdd"))
        Else
            Append(CStr(Nothing))
        End If
    End Sub

    Public Sub Append(Value As String)
        If Value IsNot Nothing Then fSB.Append(Value.Replace(vbTab, " "))
        fSB.Append(vbTab)   'Extra tabs will be trimmed
    End Sub

    Public Overrides Function ToString() As String
        Return fSB.ToString.TrimEnd(vbTab)
    End Function

End Class
