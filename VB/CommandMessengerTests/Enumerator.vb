Imports Microsoft.VisualBasic
Imports System
Imports System.Collections.Generic

Namespace CommandMessengerTests
	Public Class Enumerator
		Private ReadOnly _enumTable As Dictionary(Of String, Integer)
		Private _enumCounter As Integer
		Public Sub New()
			_enumTable = New Dictionary(Of String, Integer)()
			_enumCounter = 0
		End Sub

		Public Sub Add(ByVal enumDescriptor As String)
			_enumTable.Add(enumDescriptor, _enumCounter)
			_enumCounter += 1
		End Sub

		Public Sub Add(ByVal enumDescriptors() As String)
			For Each enumDescriptor In enumDescriptors
				Add(enumDescriptor)
			Next enumDescriptor
		End Sub

		Default Public Property Item(ByVal enumDescriptor As String) As Integer
			Get
				If _enumTable.ContainsKey(enumDescriptor) Then
					Return _enumTable(enumDescriptor)
				Else
					Throw New ArgumentException("This enum does not exist")
				End If
			End Get
			Set(ByVal value As Integer)
				_enumTable(enumDescriptor) = value
			End Set
		End Property

	End Class
End Namespace
