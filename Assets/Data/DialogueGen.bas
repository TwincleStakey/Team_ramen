Attribute VB_Name = "DialogueGen"
Option Explicit

' Korean text lives only in the sheets. This module holds logic only (ASCII).
Private Const MAX_REQ As Long = 3
Private Const MAX_AMT As Long = 3
Private Const REMOVE_P As Double = 0.15   ' chance a base ingredient becomes a remove request
Private Const LESS_P As Double = 0.1      ' chance a base ingredient becomes a less request

' ---------- sheet loading ----------
Private Function LoadSheet(sheetName As String) As Variant
    Dim ws As Worksheet
    Set ws = ThisWorkbook.Worksheets(sheetName)
    Dim lastRow As Long, lastCol As Long
    lastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    lastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    If lastRow < 2 Then lastRow = 2
    LoadSheet = ws.Range(ws.Cells(1, 1), ws.Cells(lastRow, lastCol)).Value
End Function

Private Function RndInt(lo As Long, hi As Long) As Long
    RndInt = lo + Int(Rnd() * (hi - lo + 1))
End Function

Private Function ColIndex(data As Variant, header As String) As Long
    Dim c As Long
    For c = 1 To UBound(data, 2)
        If CStr(data(1, c)) = header Then
            ColIndex = c
            Exit Function
        End If
    Next c
    ColIndex = 0
End Function

Private Function FindRow(data As Variant, keyCol As Long, key As String) As Long
    Dim r As Long
    For r = 2 To UBound(data, 1)
        If CStr(data(r, keyCol)) = key Then
            FindRow = r
            Exit Function
        End If
    Next r
    FindRow = 0
End Function

' random text among rows where col keyCol = key
Private Function PickText(data As Variant, keyCol As Long, key As String, textCol As Long) As String
    Dim matches() As Long, n As Long, r As Long
    ReDim matches(1 To UBound(data, 1))
    n = 0
    For r = 2 To UBound(data, 1)
        If CStr(data(r, keyCol)) = key Then
            n = n + 1
            matches(n) = r
        End If
    Next r
    If n = 0 Then PickText = "" Else PickText = CStr(data(matches(RndInt(1, n)), textCol))
End Function

' random text among rows where two key columns match
Private Function PickText2(data As Variant, c1 As Long, k1 As String, c2 As Long, k2 As String, textCol As Long) As String
    Dim matches() As Long, n As Long, r As Long
    ReDim matches(1 To UBound(data, 1))
    n = 0
    For r = 2 To UBound(data, 1)
        If CStr(data(r, c1)) = k1 And CStr(data(r, c2)) = k2 Then
            n = n + 1
            matches(n) = r
        End If
    Next r
    If n = 0 Then PickText2 = "" Else PickText2 = CStr(data(matches(RndInt(1, n)), textCol))
End Function

' first (canonical) text where two key columns match
Private Function FirstText2(data As Variant, c1 As Long, k1 As String, c2 As Long, k2 As String, textCol As Long) As String
    Dim r As Long
    For r = 2 To UBound(data, 1)
        If CStr(data(r, c1)) = k1 And CStr(data(r, c2)) = k2 Then
            FirstText2 = CStr(data(r, textCol))
            Exit Function
        End If
    Next r
    FirstText2 = ""
End Function

' Template: ingredient | amount | difficulty | template
' amount 1..3 = add, -1 = remove, -2 = less, Ramen/0 = ramen order line.
' Tries the target difficulty first, then steps down until something matches.
Private Function PickTemplate(tdata As Variant, ing As String, amt As Long, diff As Long) As String
    Dim matches() As Long, n As Long, r As Long, target As Long
    ReDim matches(1 To UBound(tdata, 1))
    target = diff
    If diff > 1 And Rnd() < 0.35 Then target = diff - 1
    Do
        n = 0
        For r = 2 To UBound(tdata, 1)
            If (CStr(tdata(r, 1)) = ing Or CStr(tdata(r, 1)) = "Any") And Val(tdata(r, 2)) = amt And Val(tdata(r, 3)) = target Then
                n = n + 1
                matches(n) = r
            End If
        Next r
        If n > 0 Then Exit Do
        target = target - 1
    Loop While target >= 1
    If n = 0 Then PickTemplate = "" Else PickTemplate = CStr(tdata(matches(RndInt(1, n)), 4))
End Function

' Hint: ramenType | difficulty | template | excludePersona  (difficulty <= diff, persona not excluded)
Private Function PickHint(hdata As Variant, ramen As String, diff As Long, personaId As String) As String
    Dim matches() As Long, n As Long, r As Long, excl As String
    ReDim matches(1 To UBound(hdata, 1))
    n = 0
    For r = 2 To UBound(hdata, 1)
        excl = ""
        If UBound(hdata, 2) >= 4 Then excl = CStr(hdata(r, 4))
        If CStr(hdata(r, 1)) = ramen And Val(hdata(r, 2)) <= diff And Not InList(excl, personaId) Then
            n = n + 1
            matches(n) = r
        End If
    Next r
    If n = 0 Then PickHint = "" Else PickHint = CStr(hdata(matches(RndInt(1, n)), 3))
End Function

Private Function UnitOf(udata As Variant, ing As String, amt As Long) As String
    Dim r As Long
    r = FindRow(udata, 1, ing)
    If r = 0 Or amt < 1 Then UnitOf = "" Else UnitOf = CStr(udata(r, 1 + amt))
End Function

' "a|b|c" -> one random option
Private Function PickOption(v As String) As String
    If InStr(v, "|") = 0 Then
        PickOption = v
    Else
        Dim parts As Variant
        parts = Split(v, "|")
        PickOption = Trim(parts(RndInt(0, UBound(parts))))
    End If
End Function

' ---------- slot substitution ----------
' conn = True: use the connective ending column (<slot>_c) when it exists, so the sentence flows into the next one.
Private Function Fill(tpl As String, pdata As Variant, pRow As Long, kdata As Variant, udata As Variant, ing As String, amt As Long, ramen As String, Optional conn As Boolean = False) As String
    Dim s As String, c As Long, d As String, amtIdx As Long, h As String, v As String, cc As Long
    Dim pick As String, usedConn As Boolean
    s = tpl
    If InStr(s, "{ramen_desc}") > 0 Then s = Replace(s, "{ramen_desc}", PickText2(kdata, 1, "RamenDesc", 2, ramen, 3))
    If InStr(s, "{ramen}") > 0 Then s = Replace(s, "{ramen}", PickText2(kdata, 1, "Ramen", 2, ramen, 3))
    If ing <> "" Then
        d = PickText2(kdata, 1, "IngDesc", 2, ing, 3)
        If d = "" Then d = PickText2(kdata, 1, "Ingredient", 2, ing, 3)
        s = Replace(s, "{ing_desc}", d)
        s = Replace(s, "{ing}", PickText2(kdata, 1, "Ingredient", 2, ing, 3))
        s = Replace(s, "{unit}", UnitOf(udata, ing, amt))
        amtIdx = amt
        If amtIdx < 1 Then amtIdx = 1      ' remove/less lines use the "a little" word
        c = ColIndex(pdata, "amt" & amtIdx)
        If c > 0 Then s = Replace(s, "{amt}", PickOption(CStr(pdata(pRow, c))))
    End If
    ' persona verb slots: every header from "give" onward (skip the *_c columns themselves)
    For c = ColIndex(pdata, "give") To UBound(pdata, 2)
        h = CStr(pdata(1, c))
        If Right(h, 2) <> "_c" Then
            v = CStr(pdata(pRow, c))
            usedConn = False
            If conn Then
                cc = ColIndex(pdata, h & "_c")
                If cc > 0 Then
                    If CStr(pdata(pRow, cc)) <> "" Then
                        v = CStr(pdata(pRow, cc))
                        usedConn = True
                    End If
                End If
            End If
            pick = PickOption(v)
            s = Replace(s, "{" & h & "}", pick)
            ' a connective ending reads better with a comma than a full stop ("~고요,")
            If usedConn And InStr(".!?", Right(pick, 1)) = 0 Then s = Replace(s, pick & ".", pick & ",")
        End If
    Next c
    ' punctuation cleanup: "!." -> "!", "?." -> "?", ".." + "." -> "..." (trailing-off), never more than 3 dots
    s = Replace(s, "!.", "!")
    s = Replace(s, "?.", "?")
    Do While InStr(s, "....") > 0
        s = Replace(s, "....", "...")
    Loop
    Fill = s
End Function

' ---------- helpers ----------
Private Function SplitList(csv As String) As Variant
    Dim parts As Variant, i As Long
    parts = Split(csv, ",")
    For i = LBound(parts) To UBound(parts)
        parts(i) = Trim(parts(i))
    Next i
    SplitList = parts
End Function

Private Function InList(csv As String, item As String) As Boolean
    InList = InStr("," & Replace(csv, " ", "") & ",", "," & item & ",") > 0
End Function

' True when the hint text contains a Conflict keyword whose ingredient is being ADDED in this order
Private Function HintConflicts(h As String, kdata As Variant, reqIng() As String, reqAmt() As Long, reqCount As Long) As Boolean
    Dim r As Long, i As Long
    For r = 2 To UBound(kdata, 1)
        If CStr(kdata(r, 1)) = "Conflict" Then
            If InStr(h, CStr(kdata(r, 3))) > 0 Then
                For i = 1 To reqCount
                    If reqIng(i) = CStr(kdata(r, 2)) And reqAmt(i) > 0 Then
                        HintConflicts = True
                        Exit Function
                    End If
                Next i
            End If
        End If
    Next r
    HintConflicts = False
End Function

' True when the same text already appears among lines(1..n), ignoring the final punctuation
Private Function LineUsed(s As String, lines() As String, n As Long) As Boolean
    Dim i As Long, a As String, b As String
    a = StripPunct(s)
    For i = 1 To n
        b = StripPunct(lines(i))
        If a = b Then
            LineUsed = True
            Exit Function
        End If
    Next i
    LineUsed = False
End Function

Private Function StripPunct(s As String) As String
    Dim t As String
    t = s
    Do While Len(t) > 0 And InStr(".,!?", Right(t, 1)) > 0
        t = Left(t, Len(t) - 1)
    Loop
    StripPunct = t
End Function

Private Sub Shuffle(arr As Variant)
    Dim i As Long, j As Long, tmp As Variant
    For i = LBound(arr) To UBound(arr)
        j = RndInt(i, UBound(arr))
        tmp = arr(i)
        arr(i) = arr(j)
        arr(j) = tmp
    Next i
End Sub

' ---------- main ----------
Public Sub Generate(personaId As String)
    Randomize
    Dim pdata As Variant, odata As Variant, cdata As Variant, hdata As Variant
    Dim tdata As Variant, kdata As Variant, udata As Variant, fdata As Variant, rdata As Variant
    pdata = LoadSheet("Persona")
    odata = LoadSheet("Opener")
    cdata = LoadSheet("Closer")
    hdata = LoadSheet("Hint")
    tdata = LoadSheet("Template")
    kdata = LoadSheet("Keyword")
    udata = LoadSheet("Unit")
    fdata = LoadSheet("Filler")
    rdata = LoadSheet("Ramen")

    Dim pRow As Long
    pRow = FindRow(pdata, 1, personaId)
    If pRow = 0 Then
        MsgBox "Persona not found: " & personaId
        Exit Sub
    End If

    Dim g As Worksheet
    Set g = ThisWorkbook.Worksheets("Generator")
    Dim diff As Long, includeRemove As Boolean
    If CStr(g.Range("B3").Value) = "Random" Or CStr(g.Range("B3").Value) = "" Then
        diff = RndInt(1, 3)
    Else
        diff = CLng(g.Range("B3").Value)
    End If
    includeRemove = (CStr(g.Range("B4").Value) <> "No")

    ' ramen: random row of the Ramen sheet
    Dim rRow As Long, ramen As String, addable As Variant, baseList As String
    rRow = RndInt(2, UBound(rdata, 1))
    ramen = CStr(rdata(rRow, 1))
    addable = SplitList(CStr(rdata(rRow, 2)))
    baseList = CStr(rdata(rRow, 3))
    Shuffle addable
    Dim reqCount As Long
    reqCount = RndInt(1, MAX_REQ)
    If reqCount > UBound(addable) + 1 Then reqCount = UBound(addable) + 1

    ' decide every request first (ingredient + amount) so the hint can avoid contradicting them
    Dim reqIng(1 To 12) As String, reqAmt(1 To 12) As Long
    Dim i As Long, p As Double
    For i = 1 To reqCount
        reqIng(i) = CStr(addable(i - 1))
        reqAmt(i) = RndInt(1, MAX_AMT)
        If includeRemove And InList(baseList, reqIng(i)) Then
            p = Rnd()
            If p < REMOVE_P Then
                reqAmt(i) = -1
            ElseIf p < REMOVE_P + LESS_P Then
                reqAmt(i) = -2
            End If
        End If
    Next i

    Dim lines(1 To 12) As String, n As Long
    Dim h As String, answer As String, tries As Long, cand As String
    n = 0

    ' 1. opener
    n = n + 1: lines(n) = PickText(odata, 1, personaId, 2)
    ' 2. hint (difficulty 2+); re-pick when it contradicts a request (Keyword type=Conflict)
    If diff >= 2 Then
        h = ""
        For tries = 1 To 8
            cand = PickHint(hdata, ramen, diff, personaId)
            If cand = "" Then Exit For
            If Not HintConflicts(cand, kdata, reqIng, reqAmt, reqCount) Then
                h = cand
                Exit For
            End If
        Next tries
        If h <> "" Then
            n = n + 1: lines(n) = Fill(h, pdata, pRow, kdata, udata, "", 0, ramen, Rnd() < 0.5)
        End If
    End If
    ' 3. ramen order (connective 50% - the topping lines follow); its description must not contradict a request either
    cand = ""
    For tries = 1 To 20
        cand = Fill(PickTemplate(tdata, "Ramen", 0, diff), pdata, pRow, kdata, udata, "", 0, ramen, Rnd() < 0.5)
        If Not HintConflicts(cand, kdata, reqIng, reqAmt, reqCount) Then Exit For
        cand = ""
    Next tries
    ' fallback: order by name (never contradicts anything)
    If cand = "" Then cand = Fill("{ramen} {order}.", pdata, pRow, kdata, udata, "", 0, ramen, Rnd() < 0.5)
    n = n + 1: lines(n) = cand
    answer = FirstText2(kdata, 1, "Ramen", 2, ramen, 3)
    ' 4. ingredient requests (add / remove / less); never reuse the same template twice in one order
    Dim usedTpl(1 To 12) As String, tpl As String, k As Long, dupe As Boolean
    For i = 1 To reqCount
        For tries = 1 To 6
            tpl = PickTemplate(tdata, reqIng(i), reqAmt(i), diff)
            dupe = False
            For k = 1 To i - 1
                If usedTpl(k) = tpl Then dupe = True
            Next k
            If Not dupe Then Exit For
        Next tries
        usedTpl(i) = tpl
        ' all but the last request may be connective (60%); the last one always closes the sentence
        n = n + 1: lines(n) = Fill(tpl, pdata, pRow, kdata, udata, reqIng(i), reqAmt(i), ramen, (i < reqCount) And (Rnd() < 0.6))
        answer = answer & " + " & FirstText2(kdata, 1, "Ingredient", 2, reqIng(i), 3) & " "
        If reqAmt(i) = -1 Then
            answer = answer & FirstText2(kdata, 1, "Label", 2, "Remove", 3)
        ElseIf reqAmt(i) = -2 Then
            answer = answer & FirstText2(kdata, 1, "Label", 2, "Less", 3)
        Else
            answer = answer & reqAmt(i)
        End If
    Next i
    ' 5. filler (50%), never a line already used
    If Rnd() < 0.5 And UBound(fdata, 1) >= 2 Then
        For tries = 1 To 5
            cand = Fill(CStr(fdata(RndInt(2, UBound(fdata, 1)), 1)), pdata, pRow, kdata, udata, "", 0, ramen, Rnd() < 0.5)
            If Not LineUsed(cand, lines, n) Then Exit For
            cand = ""
        Next tries
        If cand <> "" Then
            n = n + 1: lines(n) = cand
        End If
    End If
    ' 6. closer, never a line already used
    cand = PickText(cdata, 1, personaId, 2)
    For tries = 1 To 5
        If Not LineUsed(cand, lines, n) Then Exit For
        cand = PickText(cdata, 1, personaId, 2)
    Next tries
    n = n + 1: lines(n) = cand

    ' output
    g.Range("B6").Value = CStr(pdata(pRow, 2))
    g.Range("B7").Value = diff
    g.Range("B9:B18").ClearContents
    For i = 1 To n
        g.Cells(8 + i, 2).Value = lines(i)
    Next i
    g.Range("B20").Value = answer

    ' log
    Dim lg As Worksheet, lr As Long, joined As String
    Set lg = ThisWorkbook.Worksheets("Log")
    lr = lg.Cells(lg.Rows.Count, 1).End(xlUp).Row + 1
    joined = ""
    For i = 1 To n
        If i > 1 Then joined = joined & " / "
        joined = joined & lines(i)
    Next i
    lg.Cells(lr, 1).Value = Now
    lg.Cells(lr, 2).Value = CStr(pdata(pRow, 2))
    lg.Cells(lr, 3).Value = diff
    lg.Cells(lr, 4).Value = answer
    lg.Cells(lr, 5).Value = joined
End Sub

' ---------- buttons ----------
Public Sub GenerateFromButton()
    Randomize
    Dim nm As String, pid As String
    nm = CStr(Application.Caller)
    pid = Mid(nm, 5)
    If pid = "Random" Then
        Dim pdata As Variant
        pdata = LoadSheet("Persona")
        pid = CStr(pdata(RndInt(2, UBound(pdata, 1)), 1))
    End If
    Generate pid
End Sub

Public Sub SetupButtons()
    Dim g As Worksheet
    Set g = ThisWorkbook.Worksheets("Generator")
    On Error Resume Next
    g.Buttons.Delete
    On Error GoTo 0
    Dim pdata As Variant
    pdata = LoadSheet("Persona")
    Dim colW As Double, rowH As Double, gap As Double
    colW = 105: rowH = 26: gap = 6
    Dim baseLeft As Double, baseTop As Double
    baseLeft = g.Range("D3").Left
    baseTop = g.Range("D3").Top
    Dim btn As Button
    Set btn = g.Buttons.Add(baseLeft, baseTop, colW * 3 + gap * 2, rowH)
    btn.Name = "btn_Random"
    btn.Caption = "RANDOM"
    btn.OnAction = "GenerateFromButton"
    Dim r As Long, idx As Long, col As Long, rw As Long
    idx = 0
    For r = 2 To UBound(pdata, 1)
        col = idx Mod 3
        rw = idx \ 3 + 1
        Set btn = g.Buttons.Add(baseLeft + col * (colW + gap), baseTop + rw * (rowH + gap), colW, rowH)
        btn.Name = "btn_" & CStr(pdata(r, 1))
        btn.Caption = CStr(pdata(r, 2))
        btn.OnAction = "GenerateFromButton"
        idx = idx + 1
    Next r
End Sub
