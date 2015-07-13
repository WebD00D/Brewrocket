Imports System.Web.Services
Imports System.Web.Services.Protocols
Imports System.Web.Security
Imports System.ComponentModel
Imports System.Data.SqlClient
Imports System.Security.Cryptography
Imports Stripe
Imports System.IO.MemoryStream
Imports System.Threading.Tasks
Imports System.IO
Imports System.Text
Imports System.Net.Mail
Imports System.Net.Mail.SmtpClient
Imports System.Net.Mail.SmtpDeliveryFormat
Imports System.Net.Mail.SmtpDeliveryMethod
Imports System.Net.Mail.SmtpAccess
Imports System.Net.Mail.SmtpPermission
Imports System.Net.Mail.MailAddress
Imports Octokit
' To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line.
<System.Web.Script.Services.ScriptService()> _
<System.Web.Services.WebService(Namespace:="http://tempuri.org/")> _
<System.Web.Services.WebServiceBinding(ConformsTo:=WsiProfiles.BasicProfile1_1)> _
<ToolboxItem(False)> _
Public Class Engine
    Inherits System.Web.Services.WebService

    Public Class BRUser
        Public UserID As Integer
        Public Email As String
        Public Username As String
        Public Password As String
        Public LastName As String
        Public FirstName As String
        Public LastLessonCompleted As Integer
    End Class

    Dim UserList As New List(Of BRUser)

    <WebMethod()> _
    Public Function Hello(ByVal Test As String)
        Return "Hello World"
    End Function


#Region "User Settings"

    <WebMethod(True)> _
    Public Function UpdateBasicInfo(ByVal Email As String, ByVal Password As String)

        Dim MemberID As String = Session("MemberID")
        Dim hash As String = Nothing
        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable

        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.StoredProcedure
            cmd.CommandText = "sp_BRUser_CheckEmailAvailability"
            cmd.Parameters.AddWithValue("@Email", Email)
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        If dt.Rows.Count > 0 Then
            If dt.Rows(0).Item(0) = MemberID Then
                Email = Nothing
            Else
                Return 1
            End If
        End If

        If Not Trim(Password) = String.Empty Then
            Using md5Hash As MD5 = MD5.Create()
                hash = GetHash(md5Hash, Password)
            End Using
        End If


        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.StoredProcedure
            cmd.CommandText = "BRUser_UPDATE"
            cmd.Parameters.AddWithValue("@MemberID", CInt(MemberID))
            cmd.Parameters.AddWithValue("@Email", Email)
            cmd.Parameters.AddWithValue("@Pass", hash)
            cmd.ExecuteNonQuery()
        End Using

        Return 2
    End Function


    <WebMethod(True)> _
    Public Function GetUserInfo()

        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable
        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.Text
            cmd.CommandText = " SELECT * FROM BR_AllUsers WHERE UserID = " & Session("MemberID")
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        Dim PasstoUnhash = dt.Rows(0).Item(3)

        Dim u As New BRUser
        UserList.Clear()
        u.Email = dt.Rows(0).Item(1)
        u.FirstName = dt.Rows(0).Item(7)
        u.LastName = dt.Rows(0).Item(8)
        UserList.Add(u)

        Return UserList

    End Function

    <WebMethod()> _
    Public Function ResetPassword(ByVal Email As String)

        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable

        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.Text
            cmd.CommandText = "SELECT UserID,Email,Username FROM BR_AllUsers WHERE Email = '" & Email & "'"
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        If dt.Rows.Count = 1 Then

            Dim Username As String = dt.Rows(0).Item(2)
            Dim UserID As String = dt.Rows(0).Item(0)
            Dim RandomPassword As String = Membership.GeneratePassword(10, 2)

            Dim hash As String
            Using md5Hash As MD5 = MD5.Create()
                hash = GetHash(md5Hash, RandomPassword)
            End Using

            Using cmd As SqlCommand = con.CreateCommand
                cmd.Connection = con
                cmd.Connection.Open()
                cmd.CommandType = CommandType.Text
                cmd.CommandText = "UPDATE BR_AllUsers SET Password = '" & hash & "' WHERE UserID = " & UserID
                cmd.ExecuteNonQuery()
            End Using


            Dim smtpserver As New SmtpClient()
            smtpserver.Credentials = New Net.NetworkCredential("christian@brewrocket.io", "WebD00D91") 'HASH FOR LATER
            smtpserver.Port = 587
            smtpserver.Host = "mail.oak.arvixe.com"
            smtpserver.EnableSsl = False

            Dim EmailBody As String = String.Empty
            Dim reader As StreamReader = New StreamReader(HttpContext.Current.Server.MapPath("~/Email_CredentialReset.html"))

            'TO DO: Create HTML email for ForgotPassword.html 
            EmailBody = reader.ReadToEnd
            EmailBody = EmailBody.Replace("{USERNAME}", Username)
            EmailBody = EmailBody.Replace("{PASSWORD}", RandomPassword)
            Dim Emailr = New MailMessage()
            Try
                Emailr.From = New MailAddress("Christian@Brewrocket.io", "Brewrocket Support", System.Text.Encoding.UTF8)
                Emailr.To.Add(Email)
                Emailr.Bcc.Add("Christian@Brewrocket.io")
                Emailr.Subject = "Your Brewrocket Account Credentials"
                Emailr.Body = EmailBody
                Emailr.IsBodyHtml = True
                smtpserver.Send(Emailr)

            Catch ex As Exception

            End Try

            Return 1
        Else
            Return 0
        End If



        Return ""
    End Function


#End Region

#Region "New User Process"

    <WebMethod()> _
    Public Function NewUserValidation(ByVal Email As String, ByVal Username As String)

        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable

        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.StoredProcedure
            cmd.CommandText = "sp_BRUser_CheckEmailAvailability"
            cmd.Parameters.AddWithValue("@Email", Email)
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        If dt.Rows.Count > 0 Then
            Return 1
        Else
            dt.Rows.Clear()
            dt.Columns.Clear()
            dt.Dispose()

            Using cmd As SqlCommand = con.CreateCommand
                cmd.Connection = con
                cmd.Connection.Open()
                cmd.CommandType = CommandType.StoredProcedure
                cmd.CommandText = "sp_PRELAUNCH_CheckUsernameAvailability"
                cmd.Parameters.AddWithValue("@DesiredUsername", Username)
                Using da As New SqlDataAdapter
                    da.SelectCommand = cmd
                    da.Fill(dt)
                End Using
                cmd.Connection.Close()
            End Using

            If dt.Rows.Count > 0 Then
                Return 2
            Else
                Return 3
            End If

        End If

    End Function

    <WebMethod()> _
    Public Function Subscription_ProPlan(ByVal ChargeToken As String, ByVal Email As String, ByVal FirstName As String, ByVal LastName As String, ByVal Address As String)

        Dim CustyID As String = String.Empty
        Dim StripePlan As String = String.Empty

        Try
            StripeConfiguration.SetApiKey("sk_test_ES6jdki3z4hLiVZScgSQqYCl")
            Dim tokenservice As New StripeTokenService()
            Dim stripetoken As StripeToken = tokenservice.Get(ChargeToken)

            Dim myCustomer As New StripeCustomerCreateOptions()
            myCustomer.Email = Email
            myCustomer.Card = New StripeCreditCardOptions()
            myCustomer.Card.TokenId = ChargeToken

            Dim customerService As New StripeCustomerService
            Dim StripeCustomer As StripeCustomer = customerService.Create(myCustomer)
            CustyID = StripeCustomer.Id

            Dim MyCharge As New StripeChargeCreateOptions()
            MyCharge.Amount = 9900
            MyCharge.Currency = "usd"
            MyCharge.Description = "Brewrocket Test Pilot Program"
            MyCharge.CustomerId = CustyID
            MyCharge.Capture = True

            Dim chargeService = New StripeChargeService()
            Dim StripeCharge As StripeCharge = chargeService.Create(MyCharge)

            ' If charge is successful then we need to send the email to ship out a brew kit asap. 
            ' This is a preventative measure for the possibility of the rest of the process not executing after the charge has been made. 
            ' We need to know to ship a kit
            SendInternalEmail("New Signup", "Send " & FirstName & " " & LastName & " a kit to the following address: " & Address & " and set up slack account for this email:" & Email)


        Catch ex As Exception
            Return 1
        End Try

        Return CustyID
    End Function


    <WebMethod()> _
    Public Sub SendInternalEmail(ByVal Subject As String, ByVal Message As String)
        ' TO DO: Email Code to send internal message. 
        Dim smtpserver As New SmtpClient()
        smtpserver.Credentials = New Net.NetworkCredential("christian@brewrocket.io", "WebD00D91")
        smtpserver.Port = 587
        smtpserver.Host = "mail.oak.arvixe.com"
        smtpserver.EnableSsl = False

        Dim Emailr = New MailMessage()
        Try
            Emailr.From = New MailAddress("Christian@Brewrocket.io", "Brewrocket Support", System.Text.Encoding.UTF8)
            Emailr.To.Add("Christian@Brewrocket.io")
            Emailr.Bcc.Add("Sean@Brewrocket.io")
            Emailr.Subject = Subject
            Emailr.Body = Message
            Emailr.IsBodyHtml = True
            smtpserver.Send(Emailr)

        Catch ex As Exception

        End Try
    End Sub


    <WebMethod()> _
    Public Function CreateNewUser(ByVal Email As String, ByVal Username As String, ByVal Password As String, ByVal Stripe As String, ByVal FirstName As String, ByVal LastName As String)

        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)

        Dim SubscriptionStartDate As Date = Date.Today.AddDays(7)
        Dim SubscriptionEndDate As Date = SubscriptionStartDate.AddYears(1)

        Dim hash As String
        Using md5Hash As MD5 = MD5.Create()
            hash = GetHash(md5Hash, Password)
        End Using

        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.StoredProcedure
            cmd.CommandText = "sp_BRUser_CREATE"
            cmd.Parameters.AddWithValue("@Email", Email)
            cmd.Parameters.AddWithValue("@FirstName", FirstName)
            cmd.Parameters.AddWithValue("@LastName", LastName)
            cmd.Parameters.AddWithValue("@Username", Username)
            cmd.Parameters.AddWithValue("@Password", hash)
            cmd.Parameters.AddWithValue("@StripeID", Stripe)
            cmd.Parameters.AddWithValue("@IsTestPilot", 1) 'change when we go live
            cmd.ExecuteNonQuery()
            cmd.Connection.Close()

        End Using

        'Send Welcome Email

        Dim smtpserver As New SmtpClient()
        smtpserver.Credentials = New Net.NetworkCredential("christian@brewrocket.io", "WebD00D91") 'HASH FOR LATER
        smtpserver.Port = 587
        smtpserver.Host = "mail.oak.arvixe.com"
        smtpserver.EnableSsl = False

        Dim EmailBody As String = String.Empty
        Dim reader As StreamReader = New StreamReader(HttpContext.Current.Server.MapPath("~/Email_Welcome.html"))

        'TO DO: Create HTML email for ForgotPassword.html 
        EmailBody = reader.ReadToEnd
        Dim Emailr = New MailMessage()
        Try
            Emailr.From = New MailAddress("Christian@Brewrocket.io", "Brewrocket Support", System.Text.Encoding.UTF8)
            Emailr.To.Add(Email)
            Emailr.Bcc.Add("Christian@Brewrocket.io")
            Emailr.Subject = "Your account is now activated!"
            Emailr.Body = EmailBody
            Emailr.Body = EmailBody.Replace("{FirstName}", FirstName)
            Emailr.IsBodyHtml = True
            smtpserver.Send(Emailr)

        Catch ex As Exception

        End Try

        Return ""
    End Function



#End Region

#Region "User Processes"



    <WebMethod(True)> _
    Public Function OfficeHoursInvite(ByVal MeetingTime As String)

        Dim UserID As Integer = Session("MemberID")
        Dim dt As New DataTable
        Try
            Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
            Using cmd As SqlCommand = con.CreateCommand
                cmd.Connection = con
                cmd.Connection.Open()
                cmd.CommandText = "SELECT * FROM BR_AllUsers"
                cmd.CommandType = CommandType.Text
                Using da As New SqlDataAdapter
                    da.SelectCommand = cmd
                    da.Fill(dt)
                End Using
            End Using
        Catch ex As Exception
            Return 0
        End Try

        Dim Name As String = dt.Rows(0).Item("FirstName")
        Dim Email As String = dt.Rows(0).Item("Email")
        Dim HangoutSessionInfo As String = MeetingTime
        Dim HangoutLink As String = "http://www.google.com"


        Dim smtpserver As New SmtpClient()
        smtpserver.Credentials = New Net.NetworkCredential("christian@brewrocket.io", "WebD00D91") 'HASH FOR LATER
        smtpserver.Port = 587
        smtpserver.Host = "mail.oak.arvixe.com"
        smtpserver.EnableSsl = False

        Dim EmailBody As String = String.Empty
        Dim reader As StreamReader = New StreamReader(HttpContext.Current.Server.MapPath("~/Email_OfficeHoursConfirmation.html"))
      
        EmailBody = reader.ReadToEnd
        EmailBody = EmailBody.Replace("{FirstName}", Name)
        EmailBody = EmailBody.Replace("{Meeting}", HangoutSessionInfo)
        EmailBody = EmailBody.Replace("{Link}", HangoutLink)
        Dim Emailr = New MailMessage()
        Try
            Emailr.From = New MailAddress("Christian@Brewrocket.io", "Brewrocket Team", System.Text.Encoding.UTF8)
            Emailr.To.Add(Email)
            Emailr.Bcc.Add("Christian@Brewrocket.io")
            Emailr.Subject = "Brewrocket Office Hours Invite for " & HangoutSessionInfo
            Emailr.Body = EmailBody
   
            Emailr.IsBodyHtml = True
            smtpserver.Send(Emailr)

        Catch ex As Exception

        End Try

        Return 1
    End Function




    <WebMethod(True)> _
    Public Function GiveFeedback(ByVal Feedback As String)

        Dim UserID As Integer = Session("MemberID")
        Dim dt As New DataTable
        Try
            Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
            Using cmd As SqlCommand = con.CreateCommand
                cmd.Connection = con
                cmd.Connection.Open()
                cmd.CommandText = "SELECT * FROM BR_AllUsers"
                cmd.CommandType = CommandType.Text
                Using da As New SqlDataAdapter
                    da.SelectCommand = cmd
                    da.Fill(dt)
                End Using
            End Using
        Catch ex As Exception
            Return 0
        End Try

        Dim Name As String = dt.Rows(0).Item("FirstName")
        Dim Email As String = dt.Rows(0).Item("Email")


        Dim smtpserver As New SmtpClient()
        smtpserver.Credentials = New Net.NetworkCredential("christian@brewrocket.io", "WebD00D91") 'HASH FOR LATER
        smtpserver.Port = 587
        smtpserver.Host = "mail.oak.arvixe.com"
        smtpserver.EnableSsl = False

        Dim EmailBody As String = String.Empty

        EmailBody = "From: " & Name & " <br/> Email: " & Email & "<br/> Message:" & Feedback
        Dim Emailr = New MailMessage()

        Emailr.From = New MailAddress("Christian@Brewrocket.io", "Brewrocket Support", System.Text.Encoding.UTF8)
        Emailr.To.Add("support@brewrocket.io")
        Emailr.Subject = "Feedback"
        Emailr.Body = EmailBody
        Emailr.ReplyToList.Add(Email)
        Emailr.IsBodyHtml = True
        smtpserver.Send(Emailr)


        Return ""
    End Function



    <WebMethod()> _
    Public Function CheckAccessCode(ByVal AccessCode As String)

        Select Case AccessCode.ToUpper()
            Case "NBE2015"
                Return True
            Case "RANDOMCODE"
                Return True
            Case Else
                Return False
        End Select

    End Function


    <WebMethod(True)> _
    Public Function Logout()
        Try
            Session("MemberID") = String.Empty
            Return 1
        Catch ex As Exception
            Return 2
        End Try

    End Function



    <WebMethod(True)> _
    Public Function GetLastLessonCompleted()

        Dim LastLessonCompleted As Integer = Nothing
        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable
        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.Text
            cmd.CommandText = " SELECT LastLessonCompleted FROM BR_AllUsers WHERE UserID = " & Session("MemberID")
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        LastLessonCompleted = dt.Rows(0).Item(0)

        Return LastLessonCompleted
    End Function

    <WebMethod(True)> _
    Public Function UpdateLastLesson(ByVal Lesson As Integer)

        Dim LastLessonCompleted As Integer = Nothing
        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable
        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.Text
            cmd.CommandText = " UPDATE BR_AllUsers SET LastLessonCompleted = " & Lesson & " WHERE UserID = " & Session("MemberID")
            cmd.ExecuteNonQuery()
        End Using



        Return ""
    End Function

    <WebMethod(True)> _
    Public Function LoginUser(ByVal Username As String, ByVal Password As String)

        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString())
        Dim dt As New DataTable

        'Pull user credentials
        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.Text
            cmd.CommandText = "SELECT * FROM BR_AllUsers WHERE Username = '" & Username & "'"
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        If dt.Rows.Count = 0 Then 'invalid username
            Return 1
        End If

        Dim EnteredPassword As String
        Using md5Hash As MD5 = MD5.Create()
            EnteredPassword = GetHash(md5Hash, Password)
        End Using

        Dim DBPassword As String = dt.Rows(0).Item("Password")

        If UnHashIt(EnteredPassword, DBPassword) Then
            Session("MemberID") = dt.Rows(0).Item("UserID")

            Using cmd As SqlCommand = con.CreateCommand
                cmd.Connection = con
                cmd.Connection.Open()
                cmd.CommandType = CommandType.StoredProcedure
                cmd.CommandText = "sp_BRUser_UpdateLastLogin"
                cmd.Parameters.AddWithValue("@UserID", dt.Rows(0).Item("UserID"))
                cmd.ExecuteNonQuery()
            End Using

            Return 4
        Else
            Return 2 'invalid password
        End If

    End Function

    <WebMethod(True)> _
    Public Function GetUsername()
        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Dim dt As New DataTable

        If Trim(Session("MemberID")) = String.Empty Then
            Return 1
        End If

        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.Text
            cmd.CommandText = "SELECT FirstName from BR_AllUsers WHERE UserID = " & Session("MemberID")
            Using da As New SqlDataAdapter
                da.SelectCommand = cmd
                da.Fill(dt)
            End Using
            cmd.Connection.Close()
        End Using

        Return dt.Rows(0).Item(0).ToString()
    End Function



#End Region

#Region "Security"
    <WebMethod()> _
    Public Function GetHash(ByVal Hash As MD5, ByVal Input As String)

        ' Convert the input string to a byte array and compute the hash. 
        Dim data As Byte() = Hash.ComputeHash(Encoding.UTF8.GetBytes(Input))

        ' Create a new Stringbuilder to collect the bytes 
        ' and create a string. 
        Dim sBuilder As New StringBuilder()

        ' Loop through each byte of the hashed data  
        ' and format each one as a hexadecimal string. 
        Dim i As Integer
        For i = 0 To data.Length - 1
            sBuilder.Append(data(i).ToString("x2"))
        Next i

        ' Return the hexadecimal string. 
        Return sBuilder.ToString()

    End Function

    <WebMethod()> _
    Public Function UnHashIt(ByVal hashOfInput As String, ByVal ControlHash As String)

        ' Hash the input. 
        ' Dim hashOfInput As String = GetHash(md5Hash, input)

        ' Create a StringComparer an compare the hashes. 
        Dim comparer As StringComparer = StringComparer.OrdinalIgnoreCase

        If 0 = comparer.Compare(hashOfInput, ControlHash) Then
            Return True
        Else
            Return False
        End If
    End Function
#End Region

    <WebMethod()> _
    Public Function GetOnInviteList(ByVal Email As String)

        Dim con As New SqlConnection(ConfigurationManager.ConnectionStrings("connex").ConnectionString)
        Using cmd As SqlCommand = con.CreateCommand
            cmd.Connection = con
            cmd.Connection.Open()
            cmd.CommandType = CommandType.StoredProcedure
            cmd.CommandText = "sp_BETA_InsertNewInvite"
            cmd.Parameters.AddWithValue("@Email", Email)
            cmd.ExecuteNonQuery()
        End Using

        'To Do: Send Email to TestPilot@Brewrocket.io

        Return "You've been successfully added to our list!"
    End Function

End Class