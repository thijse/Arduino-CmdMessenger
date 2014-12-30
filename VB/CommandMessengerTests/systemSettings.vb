Imports Microsoft.VisualBasic
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports CommandMessenger
Imports CommandMessenger.TransportLayer

Namespace CommandMessengerTests
	Public Class systemSettings
		Private privateDescription As String
		Public Property Description() As String
			Get
				Return privateDescription
			End Get
			Set(ByVal value As String)
				privateDescription = value
			End Set
		End Property
		Private privateTransport As ITransport
		Public Property Transport() As ITransport
			Get
				Return privateTransport
			End Get
			Set(ByVal value As ITransport)
				privateTransport = value
			End Set
		End Property
		Private privateMinReceiveSpeed As Single
		Public Property MinReceiveSpeed() As Single
			Get
				Return privateMinReceiveSpeed
			End Get
			Set(ByVal value As Single)
				privateMinReceiveSpeed = value
			End Set
		End Property
		Private privateMinSendSpeed As Single
		Public Property MinSendSpeed() As Single
			Get
				Return privateMinSendSpeed
			End Get
			Set(ByVal value As Single)
				privateMinSendSpeed = value
			End Set
		End Property
		Private privateMinDirectSendSpeed As Single
		Public Property MinDirectSendSpeed() As Single
			Get
				Return privateMinDirectSendSpeed
			End Get
			Set(ByVal value As Single)
				privateMinDirectSendSpeed = value
			End Set
		End Property
		Private privateBoardType As BoardType
		Public Property BoardType() As BoardType
			Get
				Return privateBoardType
			End Get
			Set(ByVal value As BoardType)
				privateBoardType = value
			End Set
		End Property
		Private privatesendBufferMaxLength As Integer
		Public Property sendBufferMaxLength() As Integer
			Get
				Return privatesendBufferMaxLength
			End Get
			Set(ByVal value As Integer)
				privatesendBufferMaxLength = value
			End Set
		End Property
	End Class
End Namespace
