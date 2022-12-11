
Imports PayBySquareTextGenerator.LZMA

<TestClass>
Public Class LZMATests

    Dim TestIn() As Byte = {207, 152, 189, 189, 9, 49, 9, 49, 9, 49, 50, 51, 53, 46, 56, 9, 69, 85, 82, 9, 9, 54, 53, 52, 51, 50, 49, 9, 9, 9, 9, 80, 97, 121, 66, 121, 83, 113, 117, 97, 114, 101, 79, 118, 101, 114, 107, 105, 108, 108, 9, 49, 9, 67, 90, 49, 55, 50, 48, 49, 48, 48, 48, 48, 48, 48, 48, 50, 56, 48, 48, 50, 54, 54, 57, 56, 49, 9, 9, 48, 9, 48}
    Dim TestOut() As Byte = {0, 103, 166, 19, 171, 208, 72, 179, 204, 44, 111, 12, 148, 19, 206, 169, 208, 216, 62, 43, 198, 84, 8, 38, 66, 18, 126, 241, 240, 44, 110, 25, 33, 180, 89, 30, 195, 101, 152, 93, 90, 8, 43, 243, 249, 215, 40, 179, 255, 214, 165, 81, 168, 29, 37, 232, 237, 125, 207, 125, 122, 130, 39, 235, 139, 179, 227, 169, 205, 159, 56, 83, 119, 193, 209, 225}

    <TestMethod>
    Sub SimpleEncodeTest()
        Dim Enc As New Lzma1Encoder
        Dim Output() As Byte = Enc.Encode(TestIn)
        Assert.IsNotNull(Output, "Output is Nothing")
        If Not TestOut.SequenceEqual(Output) Then Assert.Fail("LZMA Encode not working")
    End Sub

    <TestMethod>
    Sub ReuseEncoderTest()
        Dim Enc As New Lzma1Encoder
        Dim OutputA() As Byte = Enc.Encode(TestIn)
        Dim OutputB() As Byte = Enc.Encode(TestIn)
        Assert.IsNotNull(OutputA, "OutputA is Nothing")
        Assert.IsNotNull(OutputB, "OutputB is Nothing")
        If Not OutputA.SequenceEqual(OutputB) Then Assert.Fail("LZMA Encode is not reusable")
    End Sub

End Class
