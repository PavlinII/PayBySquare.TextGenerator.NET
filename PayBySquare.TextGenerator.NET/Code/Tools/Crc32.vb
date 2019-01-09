Option Strict On

Imports System.Security.Cryptography

Friend Class Crc32
    Inherits HashAlgorithm

    Private Const fPolynom As UInteger = &HEDB88320UI

    Friend fTable(255) As UInteger

    Private fValue As UInteger

    Public Sub New()
        Me.HashSizeValue = 32
        Initialize()
    End Sub

    Protected Overrides Sub HashCore(array() As Byte, ibStart As Integer, cbSize As Integer)
        Dim i As Integer, ti As Byte
        For i = 0 To cbSize - 1
            ti = CByte(fValue And &HFFUI) Xor array(i)
            fValue = (fValue >> 8) Xor fTable(ti)
        Next
    End Sub

    Protected Overrides Function HashFinal() As Byte()
        Return BitConverter.GetBytes(Not fValue)
    End Function

    Public Overrides ReadOnly Property Hash() As Byte()
        Get
            Return HashFinal()
        End Get
    End Property

    Public Overrides Sub Initialize()
        Dim i, b, v As UInteger
        For i = 0 To 255
            v = i
            For b = 0 To 7
                If (v And 1) = 1 Then
                    v = (v >> 1) Xor fPolynom
                Else
                    v >>= 1
                End If
            Next
            fTable(CInt(i)) = v
        Next
        fValue = UInteger.MaxValue      '&HFFFFFFFF
    End Sub

End Class
