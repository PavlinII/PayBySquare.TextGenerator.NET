
<TestClass>
Public Class PayBySquareOverkillTests

    <TestMethod>
    Sub BasicPaymentTest()
        Dim Gen As New PayBySquareOverkill("CZ1720100000002800266981", 1235.8D, "EUR", "654321", "PayBySquareOverkill")
        Assert.AreEqual("00054000CUJ17AUG92PSOB3F1IA17JL9Q3C3SAU6AG42CGGIFROV0B3E34GR8M8UODIPGNAQ10LV7UEN52PVVLL5A6K1Q9F8TLUSUVBQG8JUN2TJSEKSR7POADRS3KF10", Gen.GeneratePayBySquareOverkillString)
    End Sub

    <TestMethod>
    Sub ComplexPaymentTest()
        Dim Gen As New PayBySquareOverkill, P As Payment
        Gen.InvoiceID = "X-4242"
        P = New Payment
        With P
            .Amount = 112.35
            .CurrencyCode = "EUR"
            .BankAccounts.Add(New BankAccount("CZ1720100000002800266981", "FIOBCZPPXXX"))
            .BankAccounts.Add(New BankAccount("CZ9120100000002000278810", "FIOBCZPPXXX"))
            .VariableSymbol = "654321"
            .ConstantSymbol = "0308"
            .SpecificSymbol = "998877"
            .PaymentNote = "PayBySquareOverkill note"
            .BeneficiaryName = "Ing. Pavel Mikula"
            .BeneficiaryAddressLine1 = "AddressOf(Pavel Mikula).FirstLine"
            .BeneficiaryAddressLine2 = "AddressOf(Pavel Mikula).SecondLine"
            .PaymentDueDate = New Date(2019, 1, 1)
        End With
        Gen.Payments.Add(P)
        Assert.AreEqual("000FC000CQQGDBVH80212G6G8MPTRE2S9TNOA5IPVOQB7V525176J4DVMIDEA6P75S6N54TM114GH1N6RF2NQKBMSI92OMEOMQNNVBSF22KQGKNP6UL6NIARGU0MR0M3KN1R3B3UUD5O68NLOJH1R00T16C8TKFS883M4H88NEHKFALKV2QLMMKC6KLQ76CDOG7BPBTUDTA4P018IPEL6M8IEFK4LR6H2HM3HS168E6MQIAG9493RVK23HF4MQ78095R25OLGLL2CORBJJVIF1MH2I08GH71O0", Gen.GeneratePayBySquareOverkillString)
    End Sub

End Class
