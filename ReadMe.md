# PayBySquare standalone QR text generator for .NET

Projekt obsahuje nezávislou knihovnu pro slovenský PayBySquare standard QR plateb.

PayBySquare standard byl vytvoøen spoleèností ADELANTE, s.r.o. s hlavním dùrazem na zbyteènou složitost a obtížnou implementaci tak, aby bìžní uživatelé QR plateb museli platit výpalné této spoleènosti. PayBySquare.TextGenerator.NET øeší tento problém pro implementace v .NET.

# PayBySquare generátor QR textù pro .NET

This project provides standalone library for Slovak PayBySquare QR payment standard.

PayBySquare standard was created by ADELANTE, s.r.o. company with main focus on unneeded complexity and difficult implementation. Main purpose is to collect ransom fees from common users of QR payments. PayBySquare.TextGenerator.NET deals with this problem for .NET implementation.

## Getting started

Library is prepared as .NET Standard 2.0. Code can be easily adjusted to any other project type.

Simple payment:
```
Dim Data As New PayBySquareTextGenerator.PayBySquareOverkill("CZ1720100000002800266981", 1235.8D, "EUR", "654321", "PayBySquareOverkill")
Dim Text As String = Data.GeneratePayBySquareOverkillString
```

Payment can be decorated with other informations
```
Dim Payment As New PayBySquareTextGenerator.Payment
With Payment
    .Amount = 112.35
    .CurrencyCode = "EUR"
    .BankAccounts.Add(New BankAccount("CZ1720100000002800266981", "FIOBCZPPXXX"))
    .VariableSymbol = "654321"
    .ConstantSymbol = "0308"
    .SpecificSymbol = "998877"
    .PaymentNote = "PayBySquareOverkill note"
    .PaymentDueDate = New Date(2019, 1, 1)
End With
Dim Data As New PayBySquareTextGenerator.PayBySquareOverkill(Payment)
Dim Text As String = Data.GeneratePayBySquareOverkillString
```

## Next step?

Transfer generated text to QR code using free online generators or custom library.

Error correction level "Medium" is suggested.

## C# implementation

Everything except LZMA namespace can be easily rewriten for C#.

LZMA namespace is based on C# managed-lzma project: [https://github.com/weltkante/managed-lzma](https://github.com/weltkante/managed-lzma)

Do NOT rewrite this LZMA namespace back to C#, that would be waste of time. Link to managed-lzma project directly (start with ManagedLzma.LZMA.AsyncEncoder) or use it's content to replace local code structure.

