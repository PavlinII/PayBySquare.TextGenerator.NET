
Public Class BankAccount

    Public IBAN, BIC As String

    Public Sub New(IBAN As String, Optional BIC As String = Nothing)
        Me.IBAN = IBAN : Me.BIC = BIC
    End Sub

End Class
