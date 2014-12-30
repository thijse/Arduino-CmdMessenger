Imports Microsoft.VisualBasic
Imports System

Namespace CommandMessengerTests
	Public Class Random
		Private Shared ReadOnly RandomNumber As New System.Random()

		Public Shared Function RandomizeFloat(ByVal min As Single, ByVal max As Single) As Single
			Return CSng(min + ((max - min) *RandomNumber.NextDouble()))
		End Function

		Public Shared Function RandomizeDouble(ByVal min As Double, ByVal max As Double) As Double
			Dim random = RandomNumber.NextDouble()
			Return CDbl(min + max * random - min * random)
		End Function


		Public Shared Function RandomizeInt16(ByVal min As Int16, ByVal max As Int16) As Int16
			Return CShort(Fix(min + ((CType(max, Double) - CType(min, Double)) *RandomNumber.NextDouble())))
		End Function

		Public Shared Function RandomizeInt32(ByVal min As Int32, ByVal max As Int32) As Int32
			Return CInt(Fix(min + ((CType(max, Double) - CType(min, Double)) *RandomNumber.NextDouble())))
		End Function

		Public Shared Function RandomizeBool() As Boolean
			Return (RandomNumber.NextDouble() > 0.5)
		End Function
	End Class
End Namespace