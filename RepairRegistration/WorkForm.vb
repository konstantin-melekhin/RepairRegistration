Imports Library3
Public Class WorkForm
    Dim PCInfo As New ArrayList() 'PCInfo = (App_ID, App_Caption, lineID, LineName, StationName,CT_ScanStep)
    Dim LOTID, PCBID As Integer, ModelName, LOTName As String
    Dim ds As New DataSet
    Dim Arr() As DataRow

    Public Property Arr1 As DataRow()
        Get
            Return Arr
        End Get
        Set(value As DataRow())
            Arr = value
        End Set
    End Property

    Private Sub WorkForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'получение данных о станции
        PCInfo = GetPCInfo(28)
        If PCInfo.Count = 0 Then
            'если не найдено, то создаем новую запись с названием этого ПК и приложения
            RunCommand("use FAS
            insert into [FAS].[dbo].[FAS_App_ListForPC] (App_ID,LineID,StationID,CreateDate,[CT_ScanStep]) 
            values (28,23," & GetStationID() & ",CURRENT_TIMESTAMP, 4)")
            PCInfo = GetPCInfo(28)
        End If
        'LabelAppName.Text = PCInfo(1)
        '    Label_StationName.Text = PCInfo(5)
        '    LB_CurrentStep.Text = PCInfo(7)
        '    Lebel_StationLine.Text = PCInfo(3)
        '    TextBox1.Text += "App_ID = " & PCInfo(0) & vbCrLf &
        '                    "App_Caption = " & PCInfo(1) & vbCrLf &
        '                    "lineID = " & PCInfo(2) & vbCrLf &
        '                    "LineName = " & PCInfo(3) & vbCrLf &
        '                    "StationID = " & PCInfo(4) & vbCrLf &
        '                    "StationName = " & PCInfo(5) & vbCrLf &
        '                    "CT_ScanStepID = " & PCInfo(6) & vbCrLf &
        '                    "CT_ScanStep = " & PCInfo(7) & vbCrLf

        'Запуск программы
        '___________________________________________________________
        GB_UserData.Location = New Point(10, 12)
        TB_RFIDIn.Focus()
    End Sub

    'регистрация пользователя
    Dim UserInfo As New ArrayList()
    Private Sub TB_RFIDIn_KeyDown(sender As Object, e As KeyEventArgs) Handles TB_RFIDIn.KeyDown
        TB_RFIDIn.MaxLength = 10
        If e.KeyCode = Keys.Enter And TB_RFIDIn.TextLength = 10 Then ' если длина номера равна 10, то запускаем процесс
            UserInfo = GetUserData(TB_RFIDIn.Text, GB_UserData, GB_WorkAria, L_UserName, TB_RFIDIn)
            'TextBox3.Text = "UserID = " & UserInfo(0) & vbCrLf &
            '            "Name = " & UserInfo(1) & vbCrLf &
            '            "User Group = " & UserInfo(2) & vbCrLf  'UserInfo
            SerialTextBox.Focus()
        ElseIf e.KeyCode = Keys.Enter Then
            TB_RFIDIn.Clear()
        End If
    End Sub 'регистрация пользователя
    Private Sub SerialTextBox_KeyDown(sender As Object, e As KeyEventArgs) Handles SerialTextBox.KeyDown
        Dim Mess As New ArrayList()
        ds = LoadDS("use fas SELECT [SMTNumberFormat], [ID],M.ModelName, l.FullLOTCode
                FROM [FAS].[dbo].[Contract_LOT] as L
                left join FAS_Models as M On M.ModelID = L.ModelID
                where L.ID>20057 and CheckFormatSN_SMT = 1")
        Arr1 = ds.Tables.Item(0).Select()
        If e.KeyCode = Keys.Enter Then
            If CheckFormatSN(SerialTextBox.Text) = True Then
                PCBID = SerchSNinLaserBase(SerialTextBox.Text)
                If PCBID <> 0 Then
                    Mess = SerchSNIDinStepRes(PCBID)

                End If
            End If
        End If
    End Sub

    Private Function CheckFormatSN(SN As String) As Boolean
        Dim Res As Boolean
        For Each item In Arr
            If SN.IndexOf(GetLOTSNFormat(item.ItemArray(0))) <> -1 Then
                LOTID = item.ItemArray(1)
                ModelName = item.ItemArray(2)
                L_Model.Text = ModelName
                LOTName = item.ItemArray(3)
                L_LOT.Text = LOTName
                Res = True
                Exit For
            Else
                PrintLabel(Controllabel, SN & " не верный номер", 12, 193, Color.Red)
                SerialTextBox.Enabled = False
                BT_Pause.Focus()
                Res = False
            End If
        Next
        Return Res
    End Function

    Private Function SerchSNinLaserBase(SN As String) As Integer
        Return SelectInt("use SMDCOMPONETS SELECT [IDLaser] FROM [SMDCOMPONETS].[dbo].[LazerBase] where Content = '" & SN & "'")
    End Function


    Private Function SerchSNIDinStepRes(SNID As String) As ArrayList
        Dim Mess As New ArrayList()
        Dim PCBStepRes As New ArrayList(SelectListString("USE FAS SELECT [StepID],[TestResult],[ScanDate],[SNID]
                            FROM [FAS].[dbo].[Ct_StepResult] where [PCBID] = " & SNID))
        If PCBStepRes.Count = 0 Then ' плата попала в ремонт, но результата теста нет
            Mess.AddRange(New ArrayList() From {"Ошибка", "Плата " & SerialTextBox.Text & " не проходила этап тестирования!", Color.Red, False})
            SerialTextBox.Enabled = False
            BT_Pause.Focus()
        ElseIf PCInfo(6) <> PCBStepRes(0) And PCBStepRes(1) = 3 Then 'Плата имеет статус x/3, то проверить опер лог и определить откуда плата
            Mess.AddRange(New ArrayList() From {"В процессе", "Плата " & SerialTextBox.Text & " зарегистрирована в ремонте!", Color.Orange, True})
            RunCommand("USE FAS Update [FAS].[dbo].[Ct_StepResult] 
                    set StepID = 4, TestResult = 1, ScanDate = CURRENT_TIMESTAMP
                    where PCBID = " & SNID)
            RunCommand("insert into [FAS].[dbo].[Ct_OperLog] ([PCBID],[LOTID],[StepID],[TestResultID],[StepDate],
                    [StepByID],[LineID]) values (" & SNID & "," & LOTID & ",4,1,CURRENT_TIMESTAMP," & UserInfo(0) & "," & PCInfo(2))

        ElseIf PCBStepRes(0) = 5 And PCBStepRes(1) = 2 Then 'Плата имеет статус 5/2
            Mess.AddRange(New ArrayList() From {"Успех", "Плата " & SerialTextBox.Text & " отремонтирована!", Color.Green, True})
            RunCommand("USE FAS Update [FAS].[dbo].[Ct_StepResult] 
                    set StepID = 4, TestResult = 2, ScanDate = CURRENT_TIMESTAMP
                    where PCBID = " & SNID)
            RunCommand("insert into [FAS].[dbo].[Ct_OperLog] ([PCBID],[LOTID],[StepID],[TestResultID],[StepDate],
                    [StepByID],[LineID]) values (" & SNID & "," & LOTID & ",4,2,CURRENT_TIMESTAMP," & UserInfo(0) & "," & PCInfo(2) & ")")
            'Если плата в таблице StepResult имеет шаг совпадающий с текущей станцией и результат равен 2
        ElseIf PCBStepRes(0) = PCInfo(6) And PCBStepRes(1) = 2 Then 'Плата имеет статус 4/2
            Mess.AddRange(New ArrayList() From {"Ошибка", "Плата " & SerialTextBox.Text & " уже отремонтирована!" &
                               vbCrLf & "Передайте плату на этап тестирования!", Color.DarkGreen, False})
            SerialTextBox.Enabled = False
            BT_Pause.Focus()
            'Если плата в таблице StepResult имеет  результат равен 3
        ElseIf PCBStepRes(0) <> PCInfo(6) And PCBStepRes(0) <> 5 And (PCBStepRes(1) = 1 Or PCBStepRes(1) = 2) Then 'Плата имеет статус x/1 или x/2 
            Mess.AddRange(New ArrayList() From {"Ошибка", "Плата " & SerialTextBox.Text & " имеет успешный последний шаг или незавершенное тестирование!" & vbCrLf &
                          "Проверьте информацию по шагам!", Color.Red, False})
            SerialTextBox.Enabled = False
            BT_Pause.Focus()
        End If
        'PrintLabel(Controllabel, Mess(), 12, 193, MesColor)
        Return Mess
    End Function

    'Кнопка очистки поля ввода номера
    Private Sub BT_CleareSN_Click(sender As Object, e As EventArgs) Handles BT_CleareSN.Click
        If GB_PCBInfoMode.Visible = False Then
            SerialTextBox.Clear()
            SerialTextBox.Enabled = True
            BT_Pass.Visible = False
            BT_Fail.Visible = False
            DG_UpLog.Visible = True
            SerialTextBox.Focus()
        Else
            TB_GetPCPInfo.Clear()
            TB_GetPCPInfo.Enabled = True
            TB_GetPCPInfo.Focus()
        End If
    End Sub

End Class
