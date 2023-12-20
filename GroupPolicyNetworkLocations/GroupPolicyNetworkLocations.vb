﻿
Imports System.Xml
Imports System.DirectoryServices
Imports Tulpep.ActiveDirectoryObjectPicker
Imports System.Text
Imports System.IO
Imports Microsoft.Win32
Imports Claunia.PropertyList

Public Class GroupPolicyNetworkLocations

    ' ******************************************************* '
    ' *           Global Constants Declared Below           * '
    ' ******************************************************* '

    ' Used in description field to store NoMAD settings in AD for the next import, so you don't need to recreate custom settings every time.
    Const DescriptionPrefix = "Automatically created by GroupPolicyNetworkLocations." + vbLf + vbLf +
                              "Please visit https://github.com/jperryNPS/GroupPolicyNetworkLocations to download the program."
    Const DataBlockStart = "##DATABLOCK_NoMAD_Settings_START##"
    Const DataBlockEnd = "##DATABLOCK_NoMAD_Settings_END##"

    ' Used in Ini Folder and Shortcut
    Const ShareImage = "1"
    Const ShareUserContext = "1"
    Const ShareBypassErrors = "1"
    const ShareRemove = "1"
    
    ' Used in Ini Folder and Shortcut Properties
    Const SharePropAction = "U"

    ' Used in Ini 1 and 2
    Const IniClsid = "{EEFACE84-D3D8-4680-8D4B-BF103E759448}"

    ' Used in Ini 1
    Const Ini1Name = "CLSID2"
    Const Ini1Status = Ini1Name

    ' Used in Ini 2
    Const Ini2Name = "Flags"
    Const Ini2Status = Ini2Name

    ' Used in Ini Properties 1 and 2
    Const IniPropPath1 = "%APPDATA%\Microsoft\Windows\Network Shortcuts\"
    Const IniPropPath3 = "\desktop.ini"
    Const IniPropSection = ".ShellClassInfo"

    ' Used in Ini Properties 1
    Const Ini1PropValue = "{0AFACED1-E828-11D1-9187-B532F1E9575D}"
    Const Ini1PropProperty = "CLSID2"

    ' Used in Ini Properties 2
    Const Ini2PropValue = "2"
    Const Ini2PropProperty = "Flags"

    ' Used in Ini Properties 3
    Const Ini3PropValue = "%WINDIR%\System32\SHELL32.dll,275"
    Const Ini3PropProperty = "IconResource"

    ' Used in Folder
    Const FolderClsid = "{07DA02F5-F9CD-4397-A550-4AE21B6B4BD3}"
    Const FolderDisabled = "0"

    ' Used in Folder Properties
    Const FolderPropDeleteFolder = "1"
    Const FolderPropDeleteSubFolders = "0"
    Const FolderPropDeleteFiles = "1"
    Const FolderPropDeleteReadOnly = "1"
    Const FolderPropDeleteIgnoreErrors = "1"
    Const FolderPropPath1 = "%APPDATA%\Microsoft\Windows\Network Shortcuts\"
    Const FolderPropReadOnly = "1"
    Const FolderPropArchive = "0"
    Const FolderPropHidden = "0"

    ' Used in Shortcut
    Const ShortcutClsid = "{4F2F7C55-2790-433e-8127-0739D1CFA327}"
    Const ShortcutName = "target"
    Const ShortcutStatus = "target"

    ' Used in Shortcut Properties
    Const ShortcutPropPid1 = ""
    Const ShortcutPropTargetType = "FILESYSTEM"
    Const ShortcutPropComment = ""
    Const ShortcutPropShortcutKey = "0"
    Const ShortcutPropStartIn = ""
    Const ShortcutPropArguments = ""
    Const ShortcutPropIconIndex = "0"
    Const ShortcutPropIconPath = ""
    Const ShortcutPropWindow = ""
    Const ShortcutPropShortcutPath1 = "%APPDATA%\Microsoft\Windows\Network Shortcuts\"
    Const ShortcutPropShortcutPath3 = "\target"

    ' Used in FilterGroup
    Const FilterGroupBool = "OR"
    Const FilterGroupNot = "0"
    Const FilterGroupUserContext = "1"
    Const FilterGroupPrimaryGroup = "0"
    Const FilterGroupLocalGroup = "0"

    ' ******************************************************* '
    ' *           Global Variables Declared Below           * '
    ' ******************************************************* '

    ' Use the Active Directory domain that the computer is joined to
    Dim objDomain As ActiveDirectory.Domain = ActiveDirectory.Domain.GetComputerDomain()
    Friend strDomainName As String = objDomain.Name

    Dim regUserSettings As RegistryKey
    Dim XMLIniFiles, XMLFolders, XMLShortcuts As New XmlDocument()
    Dim tableNetworkLocations, tableFilterGroups, tableGroupPolicies, tableMountOptions As New DataTable()
    Friend ADPicker As New DirectoryObjectPickerDialog
    Dim changesSaved As Boolean = True
    Dim boolHomeAutoMount, boolDefaultsAutoMount, boolDefaultsConnectedOnly As Boolean
    Dim strGPUID, strGPPath, strIniFilesXML, strFoldersXML, strShortcutsXML As String
    Dim arrDefaultsOptions, arrHomeOptions, arrHomeGroups As New ArrayList

    Private Function ConvertToSMB(strFilePath As String) As String
        Return "smb:" + strFilePath.Replace("\", "/")
    End Function

    Private Sub MakeDataTables()

        ' Temporary column variable declaration
        Dim column As DataColumn

        ' Creates a data table that looks something like this for storing variables related to the share folders
        ' -----------------------------------------------------------------------------------------------------------------------------------------------
        ' |    ShareName    | ShareTarget | LastModified | Ini1UID | Ini2UID | FoldersUID | ShortcutsUID | UseNoMADDefaults | AutoMount | ConnectedOnly |
        ' |-----------------|-------------|--------------|---------|---------|------------|--------------|------------------|-----------|---------------|
        ' | * Unique String |   String    |   DateTime   | String  | String  |   String   |    String    |     Boolean      |  Boolean  |    Boolean    |
        ' -----------------------------------------------------------------------------------------------------------------------------------------------

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "ShareName",
            .ReadOnly = False,
            .Unique = True
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "ShareTarget",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.DateTime"),
            .ColumnName = "LastModified",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "Ini1UID",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "Ini2UID",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "FoldersUID",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "ShortcutsUID",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.Boolean"),
            .ColumnName = "UseNoMADDefaults",
            .ReadOnly = False,
            .Unique = False,
            .DefaultValue = True
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.Boolean"),
            .ColumnName = "AutoMount",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.Boolean"),
            .ColumnName = "ConnectedOnly",
            .ReadOnly = False,
            .Unique = False
        }
        tableNetworkLocations.Columns.Add(column)

        ' Creates a data table that looks something like this for storing Share folder and Mount option connections
        ' ----------------------
        ' | ShareName | Option |
        ' |-----------|--------|
        ' |  String   | String |
        ' ----------------------

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "ShareName",
            .ReadOnly = False,
            .Unique = False
        }
        tableMountOptions.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "Option",
            .ReadOnly = False,
            .Unique = False
        }
        tableMountOptions.Columns.Add(column)

        ' Creates a data table that looks something like this for storing Share folder and Filter Group connections
        ' ------------------------------------
        ' | ShareName | GroupName | GroupSID |
        ' |-----------|-----------|----------|
        ' |  String   |  String   |  String  |
        ' ------------------------------------

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "ShareName",
            .ReadOnly = False,
            .Unique = False
        }
        tableFilterGroups.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "GroupName",
            .ReadOnly = False,
            .Unique = False
        }
        tableFilterGroups.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "GroupSID",
            .ReadOnly = False,
            .Unique = False
        }
        tableFilterGroups.Columns.Add(column)

        ' Creates a data table that looks something like this for storing a list of Group Policy Objects in the domain
        ' -----------------------------------------------------------------------------------
        ' |   PolicyGUID    | PolicyName | IniFilesExists | FoldersExists | ShortcutsExists |
        ' |-----------------|------------|----------------|---------------|-----------------|
        ' | * Unique String |   String   |    Boolean     |    Boolean    |     Boolean     |
        ' -----------------------------------------------------------------------------------


        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "PolicyGUID",
            .ReadOnly = False,
            .Unique = True
        }
        tableGroupPolicies.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.String"),
            .ColumnName = "PolicyName",
            .ReadOnly = False,
            .Unique = False
        }
        tableGroupPolicies.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.Boolean"),
            .ColumnName = "IniFilesExists",
            .ReadOnly = False,
            .Unique = False
        }
        tableGroupPolicies.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.Boolean"),
            .ColumnName = "FoldersExists",
            .ReadOnly = False,
            .Unique = False
        }
        tableGroupPolicies.Columns.Add(column)

        column = New DataColumn With {
            .DataType = System.Type.GetType("System.Boolean"),
            .ColumnName = "ShortcutsExists",
            .ReadOnly = False,
            .Unique = False
        }
        tableGroupPolicies.Columns.Add(column)

        ' Temporary primary key column
        Dim PrimaryKeyColumns(0) As DataColumn

        ' Set primary key to ShareName for Network Locations table
        PrimaryKeyColumns(0) = tableNetworkLocations.Columns("ShareName")
        tableNetworkLocations.PrimaryKey = PrimaryKeyColumns

        ' Set primary key to PolicyGUID for Group Policy table
        PrimaryKeyColumns(0) = tableGroupPolicies.Columns("PolicyGUID")
        tableGroupPolicies.PrimaryKey = PrimaryKeyColumns
    End Sub

    Private Sub RefreshView()
        If ListBoxShareNames.SelectedItems.Count <> 1 Or tableNetworkLocations.Rows.Count = 0 Then
            ' Clear all fields if there is not ONLY ONE item selected, or if there are no items available to select
            TextBoxLastModified.Clear()
            TextBoxLocationName.Clear()
            TextBoxTargetPath.Clear()
            CheckBoxAutoMount.Checked = False
            CheckBoxConnectedOnly.Checked = False
            ' Then quit trying to update the view, there's nothing to update from
            Exit Sub
        End If

        ' Set the filter for the table so that the Gridview only shows group for the selected share
        tableFilterGroups.DefaultView.RowFilter = String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue)
        DataGridViewGroups.AutoResizeColumns()

        ' Find the rows where the share name matches the selected share (There should only be one)
        Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
        If rows.Count > 0 Then
            ' If a row is found set the form fields based on the values in that row
            TextBoxLastModified.Text = rows(0)("LastModified").ToString
            TextBoxLocationName.Text = rows(0)("ShareName").ToString
            TextBoxTargetPath.Text = rows(0)("ShareTarget").ToString
            CheckBoxUseNoMADDefaults.Checked = rows(0)("UseNoMADDefaults")
            NoMADDefaultsEval()

        Else
            ' If no row is found, blank the form fields
            TextBoxLastModified.Clear()
            TextBoxLocationName.Clear()
            TextBoxTargetPath.Clear()
            CheckBoxUseNoMADDefaults.Checked = False
            CheckBoxAutoMount.Checked = False
            CheckBoxConnectedOnly.Checked = False
            ListBoxOptions.DataSource = Nothing
        End If
    End Sub

    Private Sub LaunchSettings()
        ' Check if policies have changed
        RefreshPolicyList()

        ' Set the settings dropdown list to show the found policy names, but return the guid of the selected policy
        DialogSettings.ComboBoxGroupPolicies.DataSource = tableGroupPolicies
        DialogSettings.ComboBoxGroupPolicies.DisplayMember = "PolicyName"
        DialogSettings.ComboBoxGroupPolicies.ValueMember = "PolicyGUID"

        DialogSettings.CheckBoxNoMADHomeShareAutoMount.Checked = boolHomeAutoMount
        DialogSettings.CheckBoxNoMADDefaultsAutoMount.Checked = boolDefaultsAutoMount
        DialogSettings.CheckBoxNoMADDefaultsConnectedOnly.Checked = boolDefaultsConnectedOnly

        DialogSettings.ListBoxNoMADDefaultsOptions.Items.Clear()
        DialogSettings.ListBoxNoMADDefaultsOptions.Items.AddRange(arrDefaultsOptions.ToArray)

        DialogSettings.ListBoxNoMADHomeSharesOptions.Items.Clear()
        DialogSettings.ListBoxNoMADHomeSharesOptions.Items.AddRange(arrHomeOptions.ToArray)

        Try
            DialogSettings.CheckBoxAllGroups.Checked = arrHomeGroups.Item(0).Equals("All")
        Catch
            DialogSettings.CheckBoxAllGroups.Checked = False
        End Try
        DialogSettings.ListBoxNoMADHomeSharesGroups.Items.Clear()
        DialogSettings.ListBoxNoMADHomeSharesGroups.Items.AddRange(arrHomeGroups.ToArray)

        ' Preselect the current policy, if there is one
        If strGPUID <> Nothing And strGPUID <> String.Empty Then DialogSettings.ComboBoxGroupPolicies.SelectedValue = strGPUID

        ' Show the dialog, but only make changes if OK is pressed
        If DialogSettings.ShowDialog() = DialogResult.OK Then

            strGPUID = DialogSettings.ComboBoxGroupPolicies.SelectedValue
            strGPPath = "\\" + strDomainName + "\SYSVOL\" + strDomainName + "\Policies\" + strGPUID + "\User\Preferences"
            boolHomeAutoMount = DialogSettings.CheckBoxNoMADHomeShareAutoMount.Checked
            boolDefaultsAutoMount = DialogSettings.CheckBoxNoMADDefaultsAutoMount.Checked
            boolDefaultsConnectedOnly = DialogSettings.CheckBoxNoMADDefaultsConnectedOnly.Checked

            arrDefaultsOptions.Clear()
            For Each objDefaultsOption In DialogSettings.ListBoxNoMADDefaultsOptions.Items
                arrDefaultsOptions.Add(objDefaultsOption.ToString())
            Next

            arrHomeOptions.Clear()
            For Each objHomeOption In DialogSettings.ListBoxNoMADHomeSharesOptions.Items
                arrHomeOptions.Add(objHomeOption.ToString())
            Next

            arrHomeGroups.Clear()
            For Each objHomeGroup In DialogSettings.ListBoxNoMADHomeSharesGroups.Items
                arrHomeGroups.Add(objHomeGroup.ToString())
            Next

            ' Save this to the registry for the next time the program is openned
            regUserSettings.SetValue("GPUID", strGPUID)
            regUserSettings.SetValue("HomeAutoMount", boolHomeAutoMount)
            regUserSettings.SetValue("DefaultsAutoMount", boolDefaultsAutoMount)
            regUserSettings.SetValue("DefaultsConnectedOnly", boolDefaultsConnectedOnly)
            regUserSettings.SetValue("DefaultsOptions", arrDefaultsOptions.ToArray(GetType(String)))
            regUserSettings.SetValue("HomeOptions", arrHomeOptions.ToArray(GetType(String)))
            regUserSettings.SetValue("HomeGroups", arrHomeGroups.ToArray(GetType(String)))


            ' Set the Policy Name and GUID text boxes on the main form
            TextBoxPolicyName.Text = tableGroupPolicies.Select(String.Format("PolicyGUID = '{0}'", strGPUID))(0)("PolicyName")
            TextBoxPolicyGUID.Text = strGPUID

            ' Load the policy preferences from the XML Files
            ReloadData()
        End If
    End Sub

    Private Sub MakeXMLStrings()
        ' This whole thing is a mess, but it works. I'm open to suggestions.

        ' Initialize temporary string builders
        Dim sbIniFilesXML, sbFoldersXML, sbShortcutsXML As New StringBuilder()

        ' Start with the standard xml definition
        sbIniFilesXML.AppendLine("<?xml version=""1.0"" encoding=""utf-8""?>")
        sbFoldersXML.AppendLine("<?xml version=""1.0"" encoding=""utf-8""?>")
        sbShortcutsXML.AppendLine("<?xml version=""1.0"" encoding=""utf-8""?>")

        ' Open up the XML with the main group
        sbIniFilesXML.AppendLine("<IniFiles clsid=""{694C651A-08F2-47fa-A427-34C4F62BA207}"">")
        sbFoldersXML.AppendLine("<Folders clsid=""{77CC39E7-3D16-4f8f-AF86-EC0BBEE2C861}"">")
        sbShortcutsXML.AppendLine("<Shortcuts clsid=""{872ECB34-B2EC-401b-A585-D32574AA90EE}"">")

        ' Run through all of the shares in the table
        For Each shareRow In tableNetworkLocations.Rows

            ' This is really obnoxious to look at, and I pretty much hate myself for doing it this way.
            ' The above comment applies to specifically the next 13 lines and this program in general.
            sbIniFilesXML.AppendLine(String.Format("  <Ini clsid=""{0}"" name=""{1}"" status=""{2}"" image=""{3}"" changed=""{4:yyyy-MM-dd HH:mm:ss}"" uid=""{5}"" userContext=""{6}"" bypassErrors=""{7}"" removePolicy=""{8}"">",
                                                       IniClsid, Ini1Name, Ini1Status, ShareImage, shareRow("LastModified"), shareRow("Ini1UID"), ShareUserContext, ShareBypassErrors, ShareRemove))
            sbFoldersXML.AppendLine(String.Format("  <Folder clsid=""{0}"" name=""{1}"" status=""{2}"" image=""{3}"" changed=""{4:yyyy-MM-dd HH:mm:ss}"" uid=""{5}"" disabled=""{6}"" desc=""{7}"" userContext=""{8}"" bypassErrors=""{9}"" removePolicy=""{10}"">",
                                                           FolderClsid, shareRow("ShareName"), shareRow("ShareName"), ShareImage, shareRow("LastModified"), shareRow("FoldersUID"), FolderDisabled, GenerateDescription(shareRow), ShareUserContext, ShareBypassErrors, ShareRemove))
            sbShortcutsXML.AppendLine(String.Format("  <Shortcut clsid=""{0}"" name=""{1}"" status=""{2}"" image=""{3}"" changed=""{4:yyyy-MM-dd HH:mm:ss}"" uid=""{5}"" userContext=""{6}"" bypassErrors=""{7}"" removePolicy=""{8}"">",
                                                    ShortcutClsid, ShortcutName, ShortcutStatus, ShareImage, shareRow("LastModified"), shareRow("ShortcutsUID"), ShareUserContext, ShareBypassErrors, ShareRemove))

            sbIniFilesXML.AppendLine(String.Format("    <Properties path=""{0}{1}{2}"" section=""{3}"" value=""{4}"" property=""{5}"" action=""{6}""/>",
                                               IniPropPath1, shareRow("ShareName"), IniPropPath3, IniPropSection, Ini1PropValue, Ini1PropProperty, SharePropAction))
            sbFoldersXML.AppendLine(String.Format("    <Properties deleteFolder=""{0}"" deleteSubFolders=""{1}"" deleteFiles=""{2}"" deleteReadOnly=""{3}"" deleteIgnoreErrors=""{4}"" action=""{5}"" path=""{6}{7}"" readOnly=""{8}"" archive=""{9}"" hidden=""{10}""/>",
                                                 FolderPropDeleteFolder, FolderPropDeleteSubFolders, FolderPropDeleteFiles, FolderPropDeleteReadOnly, FolderPropDeleteIgnoreErrors, SharePropAction, FolderPropPath1, shareRow("ShareName"), FolderPropReadOnly, FolderPropArchive, FolderPropHidden))
            sbShortcutsXML.AppendLine(String.Format("    <Properties pidl=""{0}"" targetType=""{1}"" action=""{2}"" comment=""{3}"" shortcutKey=""{4}"" startIn=""{5}"" arguments=""{6}"" iconIndex=""{7}"" targetPath=""{8}"" iconPath=""{9}"" window=""{10}"" shortcutPath=""{11}{12}{13}""/>",
                                                   ShortcutPropPid1, ShortcutPropTargetType, SharePropAction, ShortcutPropComment, ShortcutPropShortcutKey, ShortcutPropStartIn, ShortcutPropArguments, ShortcutPropIconIndex, shareRow("ShareTarget"), ShortcutPropIconPath, ShortcutPropWindow, ShortcutPropShortcutPath1, shareRow("ShareName"), ShortcutPropShortcutPath3))

            ' Group for filters.

            If tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName"))).Count() > 0 Then
                sbIniFilesXML.AppendLine("    <Filters>")
                sbFoldersXML.AppendLine("    <Filters>")
                sbShortcutsXML.AppendLine("    <Filters>")

                Dim i As Integer = 1 ' A counter, even though I only need to know which is the first
                For Each groupRow In tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName")))
                    Dim strBool As String = "AND"
                    If i > 1 Then strBool = FilterGroupBool ' The first filter is always AND
                    ' Create a string, because all of them get the same thing
                    Dim strFilterGroup As String = String.Format("      <FilterGroup bool=""{0}"" not=""{1}"" name=""{2}"" sid=""{3}"" userContext=""{4}"" primaryGroup=""{5}"" localGroup=""{6}""/>",
                                                                 strBool, FilterGroupNot, groupRow("GroupName"), groupRow("GroupSID"), FilterGroupUserContext, FilterGroupPrimaryGroup, FilterGroupLocalGroup)
                    ' Write it to all of them
                    sbIniFilesXML.AppendLine(strFilterGroup)
                    sbFoldersXML.AppendLine(strFilterGroup)
                    sbShortcutsXML.AppendLine(strFilterGroup)

                    i += 1 ' Increase the counter
                Next

                ' Close filter group
                sbIniFilesXML.AppendLine("    </Filters>")
                sbFoldersXML.AppendLine("    </Filters>")
                sbShortcutsXML.AppendLine("    </Filters>")
            End If

            ' close the groups
            sbIniFilesXML.AppendLine("  </Ini>")
            sbFoldersXML.AppendLine("  </Folder>")
            sbShortcutsXML.AppendLine("  </Shortcut>")

            ' Do it again for the second IniFile setting
            sbIniFilesXML.AppendLine(String.Format("  <Ini clsid=""{0}"" name=""{1}"" status=""{2}"" image=""{3}"" changed=""{4:yyyy-MM-dd HH:mm:ss}"" uid=""{5}"" userContext=""{6}"" bypassErrors=""{7}"" removePolicy=""{8}"">",
                                                       IniClsid, Ini2Name, Ini2Status, ShareImage, shareRow("LastModified"), shareRow("Ini2UID"), ShareUserContext, ShareBypassErrors, ShareRemove))
            sbIniFilesXML.AppendLine(String.Format("    <Properties path=""{0}{1}{2}"" section=""{3}"" value=""{4}"" property=""{5}"" action=""{6}""/>",
                                               IniPropPath1, shareRow("ShareName"), IniPropPath3, IniPropSection, Ini2PropValue, Ini2PropProperty, SharePropAction))

            If tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName"))).Count() > 0 Then
                sbIniFilesXML.AppendLine("    <Filters>")
                Dim i As Integer = 1 ' A counter, even though I only need to know which is the first
                For Each groupRow In tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName")))
                    Dim strBool As String = "AND"
                    If i > 1 Then strBool = "OR"
                    Dim strFilterGroup As String = String.Format("      <FilterGroup bool=""{0}"" not=""0"" name=""{1}"" sid=""{2}"" userContext=""1"" primaryGroup=""0"" localGroup=""0""/>",
                                                                 strBool, groupRow("GroupName"), groupRow("GroupSID"))
                    sbIniFilesXML.AppendLine(strFilterGroup)
                    i += 1
                Next
                sbIniFilesXML.AppendLine("    </Filters>")
            End If

            sbIniFilesXML.AppendLine("  </Ini>")

                        ' Do it again for the third IniFile setting
            sbIniFilesXML.AppendLine(String.Format("  <Ini clsid=""{0}"" name=""{1}"" status=""{2}"" image=""{3}"" changed=""{4:yyyy-MM-dd HH:mm:ss}"" uid=""{5}"" userContext=""{6}"" bypassErrors=""{7}"" removePolicy=""{8}"">",
                                                       IniClsid, Ini2Name, Ini2Status, ShareImage, shareRow("LastModified"), shareRow("Ini2UID"), ShareUserContext, ShareBypassErrors, ShareRemove))
            sbIniFilesXML.AppendLine(String.Format("    <Properties path=""{0}{1}{2}"" section=""{3}"" value=""{4}"" property=""{5}"" action=""{6}""/>",
                                               IniPropPath1, shareRow("ShareName"), IniPropPath3, IniPropSection, Ini3PropValue, Ini3PropProperty, SharePropAction))

            If tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName"))).Count() > 0 Then
                sbIniFilesXML.AppendLine("    <Filters>")
                Dim i As Integer = 1 ' A counter, even though I only need to know which is the first
                For Each groupRow In tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName")))
                    Dim strBool As String = "AND"
                    If i > 1 Then strBool = "OR"
                    Dim strFilterGroup As String = String.Format("      <FilterGroup bool=""{0}"" not=""0"" name=""{1}"" sid=""{2}"" userContext=""1"" primaryGroup=""0"" localGroup=""0""/>",
                                                                 strBool, groupRow("GroupName"), groupRow("GroupSID"))
                    sbIniFilesXML.AppendLine(strFilterGroup)
                    i += 1
                Next
                sbIniFilesXML.AppendLine("    </Filters>")
            End If

            sbIniFilesXML.AppendLine("  </Ini>")
        Next

        ' Close the main group
        sbIniFilesXML.AppendLine("</IniFiles>")
        sbFoldersXML.AppendLine("</Folders>")
        sbShortcutsXML.AppendLine("</Shortcuts>")

        ' Write the values to strings
        strIniFilesXML = sbIniFilesXML.ToString
        strFoldersXML = sbFoldersXML.ToString
        strShortcutsXML = sbShortcutsXML.ToString
    End Sub

    Private Function GenerateDescription(shareInfo As DataRow) As String
        Dim DescriptionString As String

        DescriptionString = DescriptionPrefix + vbLf + vbLf + DataBlockStart + vbLf
        If shareInfo("UseNoMADDefaults") Then
            DescriptionString += "UseNoMADDefaults=True"
        Else
            DescriptionString += "UseNoMADDefaults=False" + vbLf + "AutoMount=" + shareInfo("AutoMount").ToString() + vbLf + "ConnectedOnly=" + shareInfo("ConnectedOnly").ToString()

            If tableMountOptions.Select(String.Format("ShareName = '{0}'", shareInfo("ShareName"))).Count() > 0 Then
                DescriptionString += vbLf + "Options="
                For Each optionRow In tableMountOptions.Select(String.Format("ShareName = '{0}'", shareInfo("ShareName")))
                    If (DescriptionString(DescriptionString.Length - 1) <> "=") Then DescriptionString += ","
                    DescriptionString += optionRow("Option").ToString()
                Next
            End If

        End If
        DescriptionString += vbLf + DataBlockEnd

        Return DescriptionString
    End Function

    Private Sub ReloadData()

        ' Clear out the tables
        tableNetworkLocations.Clear()
        tableFilterGroups.Clear()
        tableMountOptions.Clear()

        If File.Exists(strGPPath + "\IniFiles\IniFiles.xml") Then
            ' Load the file if it exists
            XMLIniFiles.Load(strGPPath + "\IniFiles\IniFiles.xml")
            For Each Ini As XmlNode In XMLIniFiles.SelectNodes("/IniFiles/Ini")
                ' Get values from Ini Items
                Dim strUID As String = Ini.Attributes("uid").Value
                Dim timeModified As DateTime = Ini.Attributes("changed").Value
                Dim strIniProp As String = Ini.Attributes("name").Value
                Dim strPath As String = Ini.SelectSingleNode("Properties").Attributes("path").Value
                Dim arrPath() As String = strPath.Split("\")
                Dim strShareName As String
                If arrPath.Count >= 2 Then
                    strShareName = arrPath(arrPath.Count - 2)
                Else
                    strShareName = arrPath(0)
                End If
                Dim strRowName As String = ""
                ' Determine which Ini item to use
                If strIniProp = "CLSID2" Then
                    strRowName = "Ini1UID"
                ElseIf strIniProp = "Flags" Then
                    strRowName = "Ini2UID"
                End If
                If strRowName <> "" Then
                    Dim row As DataRow
                    ' Add data to row
                    Try
                        row = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", strShareName))(0)
                        row(strRowName) = strUID.ToUpper
                        ' Only update time modified if it is later
                        If timeModified > row("LastModified") Then row("LastModified") = timeModified
                    Catch
                        ' Row doesn't exist, create it now
                        row = tableNetworkLocations.NewRow
                        row("ShareName") = strShareName
                        row(strRowName) = strUID.ToUpper
                        row("LastModified") = timeModified
                        tableNetworkLocations.Rows.Add(row)
                    End Try
                    If Ini.ChildNodes.Count > 1 Then
                        ' Add all Filters to filter table
                        For Each FilterGroup As XmlNode In Ini.SelectSingleNode("Filters").SelectNodes("FilterGroup")
                            Dim strGroupName As String = FilterGroup.Attributes("name").Value
                            Dim strGroupSID As String = FilterGroup.Attributes("sid").Value
                            Try
                                row = tableFilterGroups.Select(String.Format("ShareName = '{0}' And GroupName = '{1}' And GroupSID = '{2}'", strShareName, strGroupName, strGroupSID))(0)
                            Catch
                                row = tableFilterGroups.NewRow
                                row("ShareName") = strShareName
                                row("GroupName") = strGroupName
                                row("GroupSID") = strGroupSID
                                tableFilterGroups.Rows.Add(row)
                            End Try
                        Next
                    End If
                End If
            Next
        End If

        If File.Exists(strGPPath + "\Folders\Folders.xml") Then
            XMLFolders.Load(strGPPath + "\Folders\Folders.xml")
            For Each Folder As XmlNode In XMLFolders.SelectNodes("/Folders/Folder")
                Dim strUID As String = Folder.Attributes("uid").Value
                Dim timeModified As DateTime = Folder.Attributes("changed").Value
                Dim strShareName As String = Folder.Attributes("name").Value
                Dim strDescription As String
                Try
                    strDescription = Folder.Attributes("desc").Value
                Catch
                    strDescription = ""
                End Try
                Dim rowNetLoc, rowGroup, rowOption As DataRow
                ' Add data to row
                Try
                    rowNetLoc = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", strShareName))(0)
                    rowNetLoc("FoldersUID") = strUID.ToUpper
                    ' Only update time modified if it is later
                    If timeModified > rowNetLoc("LastModified") Then rowNetLoc("LastModified") = timeModified
                Catch
                    ' Row doesn't exist, create it now
                    rowNetLoc = tableNetworkLocations.NewRow
                    rowNetLoc("ShareName") = strShareName
                    rowNetLoc("FoldersUID") = strUID.ToUpper
                    rowNetLoc("LastModified") = timeModified
                    tableNetworkLocations.Rows.Add(rowNetLoc)
                End Try
                ' Add all Filters to filter table
                If Folder.ChildNodes.Count > 1 Then
                    For Each FilterGroup As XmlNode In Folder.SelectSingleNode("Filters").SelectNodes("FilterGroup")
                        Dim strGroupName As String = FilterGroup.Attributes("name").Value
                        Dim strGroupSID As String = FilterGroup.Attributes("sid").Value
                        Try
                            rowGroup = tableFilterGroups.Select(String.Format("ShareName = '{0}' And GroupName = '{1}' And GroupSID = '{2}'", strShareName, strGroupName, strGroupSID))(0)
                        Catch
                            rowGroup = tableFilterGroups.NewRow
                            rowGroup("ShareName") = strShareName
                            rowGroup("GroupName") = strGroupName
                            rowGroup("GroupSID") = strGroupSID
                            tableFilterGroups.Rows.Add(rowGroup)
                        End Try
                    Next
                End If
                ' Read NoMAD Settings from description field
                Dim processLine As Boolean = False
                For Each line In strDescription.Split(vbLf)
                    If (line = DataBlockEnd) Then
                        ' Check if this is the end of the dataset
                        processLine = False
                    ElseIf (processLine) Then
                        Dim strArr() As String = line.Split("=")
                        If (strArr(0) = "Options") Then
                            Dim strArrOptions() As String = strArr(1).Split(",")
                            For Each strOption In strArrOptions
                                Try
                                    rowOption = tableMountOptions.Select(String.Format("ShareName = '{0}' And Option = '{1}'", strShareName, strOption))(0)
                                Catch
                                    rowOption = tableMountOptions.NewRow
                                    rowOption("ShareName") = strShareName
                                    rowOption("Option") = strOption
                                    tableMountOptions.Rows.Add(rowOption)
                                End Try
                            Next
                        Else
                            rowNetLoc(strArr(0)) = strArr(1)
                        End If
                    ElseIf (line = DataBlockStart) Then
                        ' Set if we will process the next line or not
                        processLine = True
                    End If
                Next


            Next
        End If

        If File.Exists(strGPPath + "\Shortcuts\Shortcuts.xml") Then
            XMLShortcuts.Load(strGPPath + "\Shortcuts\Shortcuts.xml")
            For Each Shortcut As XmlNode In XMLShortcuts.SelectNodes("/Shortcuts/Shortcut")
                Dim strUID As String = Shortcut.Attributes("uid").Value
                Dim timeModified As DateTime = Shortcut.Attributes("changed").Value
                Dim strTargetPath As String = Shortcut.SelectSingleNode("Properties").Attributes("targetPath").Value
                Dim strPath As String = Shortcut.SelectSingleNode("Properties").Attributes("shortcutPath").Value
                Dim arrPath() As String = strPath.Split("\")
                Dim strShareName As String
                If arrPath.Count >= 2 Then
                    strShareName = arrPath(arrPath.Count - 2)
                Else
                    strShareName = arrPath(0)
                End If
                Dim row As DataRow
                ' Add data to row
                Try
                    row = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", strShareName))(0)
                    row("ShortcutsUID") = strUID.ToUpper
                    row("ShareTarget") = strTargetPath
                    ' Only update time modified if it is later
                    If timeModified > row("LastModified") Then row("LastModified") = timeModified
                Catch
                    ' Row doesn't exist, create it now
                    row = tableNetworkLocations.NewRow
                    row("ShareName") = strShareName
                    row("ShortcutsUID") = strUID.ToUpper
                    row("ShareTarget") = strTargetPath
                    row("LastModified") = timeModified
                    tableNetworkLocations.Rows.Add(row)
                End Try
                ' Add all Filters to filter table
                If Shortcut.ChildNodes.Count > 1 Then
                    For Each FilterGroup As XmlNode In Shortcut.SelectSingleNode("Filters").SelectNodes("FilterGroup")
                        Dim strGroupName As String = FilterGroup.Attributes("name").Value
                        Dim strGroupSID As String = FilterGroup.Attributes("sid").Value
                        Try
                            row = tableFilterGroups.Select(String.Format("ShareName = '{0}' And GroupName = '{1}' And GroupSID = '{2}'", strShareName, strGroupName, strGroupSID))(0)
                        Catch
                            row = tableFilterGroups.NewRow
                            row("ShareName") = strShareName
                            row("GroupName") = strGroupName
                            row("GroupSID") = strGroupSID
                            tableFilterGroups.Rows.Add(row)
                        End Try
                    Next
                End If
            Next
        End If

        ' Run through the table and create any missing UIDs
        For Each row In tableNetworkLocations.Rows
            If row("Ini1UID").ToString.Length = 0 Then row("Ini1UID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
            If row("Ini2UID").ToString.Length = 0 Then row("Ini2UID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
            If row("FoldersUID").ToString.Length = 0 Then row("FoldersUID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
            If row("ShortcutsUID").ToString.Length = 0 Then row("ShortcutsUID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
        Next

        ' Set listbox source
        tableNetworkLocations.DefaultView.Sort = "ShareName ASC"
        ListBoxShareNames.DataSource = tableNetworkLocations
        ListBoxShareNames.DisplayMember = "ShareName"
        ListBoxShareNames.ValueMember = "ShareName"

        ' Set source for data grid view and filter
        DataGridViewGroups.DataSource = tableFilterGroups
        tableFilterGroups.DefaultView.RowFilter = String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue)
        DataGridViewGroups.Columns("ShareName").Visible = False
        DataGridViewGroups.AutoResizeColumns()

        ' Load info into form
        RefreshView()

    End Sub

    Private Sub ChangeMade(row As DataRow)
        row("LastModified") = Now
        changesSaved = False
    End Sub

    Friend Sub RefreshPolicyList()
        ' Remove any old polices from the table
        tableGroupPolicies.Clear()

        ' Populate the group policy table with all group policies in the domain
        Using searcher = New DirectorySearcher(objDomain.GetDirectoryEntry, "(&(objectClass=groupPolicyContainer))", {"displayName", "name"}, SearchScope.Subtree)
            Dim results As SearchResultCollection = searcher.FindAll
            For Each result As SearchResult In results
                Dim directoryEntry As DirectoryEntry = result.GetDirectoryEntry
                Dim row As DataRow
                Try
                    row = tableGroupPolicies.Select(String.Format("PolicyGUID = '{0}'", directoryEntry.Properties("name").Value))(0)
                Catch
                    row = tableGroupPolicies.NewRow
                    Dim strTempGPPath As String = "\\" + strDomainName + "\SYSVOL\" + strDomainName + "\Policies\" + directoryEntry.Properties("name").Value + "\User\Preferences"
                    row("PolicyGUID") = directoryEntry.Properties("name").Value
                    row("PolicyName") = directoryEntry.Properties("displayName").Value
                    ' Check if the needed group policy settings files already exist
                    row("IniFilesExists") = File.Exists(strTempGPPath + "\IniFiles\IniFiles.xml")
                    row("FoldersExists") = File.Exists(strTempGPPath + "\Folders\Folders.xml")
                    row("ShortcutsExists") = File.Exists(strTempGPPath + "\Shortcuts\Shortcuts.xml")
                    tableGroupPolicies.Rows.Add(row)
                End Try
            Next
        End Using

        tableGroupPolicies.DefaultView.RowFilter = "IniFilesExists = TRUE AND FoldersExists = TRUE AND ShortcutsExists = TRUE"
        tableGroupPolicies.DefaultView.Sort = "PolicyName ASC"
    End Sub

    Private Sub NoMADDefaultsEval()
        ButtonAddOption.Enabled = Not CheckBoxUseNoMADDefaults.Checked
        ButtonDeleteOption.Enabled = Not CheckBoxUseNoMADDefaults.Checked
        CheckBoxAutoMount.Enabled = Not CheckBoxUseNoMADDefaults.Checked
        CheckBoxConnectedOnly.Enabled = Not CheckBoxUseNoMADDefaults.Checked
        ListBoxOptions.Enabled = Not CheckBoxUseNoMADDefaults.Checked

        If CheckBoxUseNoMADDefaults.Checked = True Then
            ListBoxOptions.DataSource = arrDefaultsOptions
            CheckBoxAutoMount.Checked = boolDefaultsAutoMount
            CheckBoxConnectedOnly.Checked = boolDefaultsConnectedOnly
            ListBoxOptions.SelectedItem = Nothing
        Else
            tableMountOptions.DefaultView.RowFilter() = String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue)
            ListBoxOptions.DataSource = tableMountOptions
            ListBoxOptions.DisplayMember = "Option"
            ListBoxOptions.ValueMember = "Option"
            Dim row As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))(0)
            If row("AutoMount").GetType Is GetType(DBNull) Then row("AutoMount") = boolDefaultsAutoMount
            If row("ConnectedOnly").GetType Is GetType(DBNull) Then row("ConnectedOnly") = boolDefaultsConnectedOnly
            CheckBoxAutoMount.Checked = row("AutoMount")
            CheckBoxConnectedOnly.Checked = row("ConnectedOnly")
        End If
    End Sub

    Private Sub GroupPolicyNetworkLocations_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If Not changesSaved Then
            ' Ask if they really want to quit, their changes haven't been saved
            If MessageBox.Show("Are you sure you want to quit?" + vbCrLf + "Your changes will Not be saved.", "Are you sure?", MessageBoxButtons.YesNoCancel) <> DialogResult.Yes Then
                e.Cancel = True
            End If
        End If
    End Sub

    Private Sub GroupPolicyNetworkLocations_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Set preferences for AD Object picker
        ADPicker.AllowedObjectTypes = ObjectTypes.Groups
        ADPicker.DefaultObjectTypes = ObjectTypes.Groups
        ADPicker.AllowedLocations = Locations.JoinedDomain
        ADPicker.DefaultLocations = Locations.JoinedDomain

        ' Create the data tables
        MakeDataTables()

        ' Load list of all policies in domain
        RefreshPolicyList()

        ' Open the registry settings for the application
        regUserSettings = Registry.CurrentUser.OpenSubKey("Software\GroupPolicyNetworkLocations", True)

        If regUserSettings Is Nothing Then
            ' Create the registry settings if they do not exist
            regUserSettings = Registry.CurrentUser.CreateSubKey("Software\GroupPolicyNetworkLocations")
        ElseIf strGPUID Is Nothing Or strGPUID = String.Empty Then
            Try
                ' Attempt to set the current policy guid from the registry
                strGPUID = regUserSettings.GetValue("GPUID").ToString()
                strGPPath = "\\" + strDomainName + "\SYSVOL\" + strDomainName + "\Policies\" + strGPUID + "\User\Preferences"
                TextBoxPolicyName.Text = tableGroupPolicies.Select(String.Format("PolicyGUID = '{0}'", strGPUID))(0)("PolicyName")
                TextBoxPolicyGUID.Text = strGPUID
            Catch
            End Try
            Try
                boolHomeAutoMount = regUserSettings.GetValue("HomeAutoMount")
            Catch
                boolHomeAutoMount = True
            End Try
            Try
                boolDefaultsAutoMount = regUserSettings.GetValue("DefaultsAutoMount")
            Catch
                boolDefaultsAutoMount = True
            End Try
            Try
                boolDefaultsConnectedOnly = regUserSettings.GetValue("DefaultsConnectedOnly")
            Catch
                boolDefaultsConnectedOnly = True
            End Try
            Try
                For Each strDefaultOption In regUserSettings.GetValue("DefaultsOptions")
                    arrDefaultsOptions.Add(strDefaultOption)
                Next
            Catch
            End Try
            Try
                For Each strHomeOption In regUserSettings.GetValue("HomeOptions")
                    arrHomeOptions.Add(strHomeOption)
                Next
            Catch
            End Try
            Try
                For Each strHomeGroup In regUserSettings.GetValue("HomeGroups")
                    arrHomeGroups.Add(strHomeGroup)
                Next
            Catch
                arrHomeGroups.Add("All")
            End Try
        Else
            ' Set the registry setting to the current policy UID
            regUserSettings.SetValue("GPUID", strGPUID)
        End If

        Do While strGPUID Is Nothing Or strGPUID = String.Empty
            ' Force user to select a group policy
            LaunchSettings()
            If strGPUID Is Nothing Or strGPUID = String.Empty Then
                MessageBox.Show("You must select a group policy on your first launch.", "First Launch", MessageBoxButtons.OK)
            End If
        Loop

        ' Load data from the group policy
        ReloadData()

    End Sub

    Private Sub ListBoxShareNames_SelectedValueChanged(sender As Object, e As EventArgs) Handles ListBoxShareNames.SelectedValueChanged
        ' Load share information
        RefreshView()
    End Sub

    Private Sub QuitToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles QuitToolStripMenuItem.Click
        Close()
    End Sub

    Private Sub ButtonDiscardChanges_Click(sender As Object, e As EventArgs) Handles ButtonDiscardChanges.Click
        Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
        If rows.Count > 0 Then
            ' Reset to values in data table
            TextBoxLocationName.Text = rows(0)("ShareName")
            TextBoxTargetPath.Text = rows(0)("ShareTarget")
        Else
            ' Clear values, share not found
            TextBoxLocationName.Clear()
            TextBoxTargetPath.Clear()
        End If
    End Sub

    Private Sub ButtonSaveChanges_Click(sender As Object, e As EventArgs) Handles ButtonSaveChanges.Click
        Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
        If rows.Count > 0 Then
            If rows(0)("ShareTarget") <> TextBoxTargetPath.Text.ToString Then
                ' Update ShareTarget only if it changed
                rows(0)("ShareTarget") = TextBoxTargetPath.Text.ToString
                ChangeMade(rows(0))
            End If
            If rows(0)("ShareName") <> TextBoxLocationName.Text.ToString Then
                ' Update ShareName only if it changed
                Dim groupRows() As DataRow = tableFilterGroups.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
                If groupRows.Count > 0 Then
                    For Each row As DataRow In groupRows
                        ' Update the ShareName for all filter Groups as well
                        row("ShareName") = TextBoxLocationName.Text.ToString
                    Next
                End If
                rows(0)("ShareName") = TextBoxLocationName.Text.ToString
                ChangeMade(rows(0))
            End If
        End If
    End Sub

    Private Sub ExportToNoMADPLISTToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExportToNoMADPLISTToolStripMenuItem.Click

        Dim arrHomeMountGroups As New NSArray()
        For Each strGroupName In arrHomeGroups.ToArray
            arrHomeMountGroups.Add(strGroupName)
        Next

        Dim arrHomeMountOptions As New NSArray()
        For Each strOption In arrHomeOptions.ToArray
            arrHomeMountOptions.Add(strOption)
        Next

        Dim dictHomeMount As New NSDictionary From {
            {"Groups", arrHomeMountGroups},
            {"Mount", boolHomeAutoMount},
            {"Options", arrHomeMountOptions}
        }


        Dim arrShares As New NSArray()
        For Each shareRow In tableNetworkLocations.Rows

            Dim arrGroups As New NSArray()
            If tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName"))).Count() > 0 Then
                For Each groupRow In tableFilterGroups.Select(String.Format("ShareName = '{0}'", shareRow("ShareName")))
                    arrGroups.Add(groupRow("GroupName").ToString().Substring(groupRow("GroupName").ToString().IndexOf("\") + 1))
                Next
            End If

            Dim arrOptions As New NSArray()
            If shareRow("UseNoMADDefaults") Then
                For Each strOption In arrDefaultsOptions.ToArray
                    arrOptions.Add(strOption)
                Next
            Else
                If tableMountOptions.Select(String.Format("ShareName = '{0}'", shareRow("ShareName"))).Count() > 0 Then
                    For Each optionRow In tableMountOptions.Select(String.Format("ShareName = '{0}'", shareRow("ShareName")))
                        arrOptions.Add(optionRow("Option").ToString())
                    Next
                End If
            End If

            Dim dictShare As New NSDictionary From {
                {"Options", arrOptions},
                {"URL", ConvertToSMB(shareRow("ShareTarget"))},
                {"Groups", arrGroups},
                {"Name", shareRow("ShareName")}
            }
            If shareRow("UseNoMADDefaults") Then
                dictShare.Add("AutoMount", boolDefaultsAutoMount)
                dictShare.Add("ConnectedOnly", boolDefaultsConnectedOnly)
            Else
                dictShare.Add("AutoMount", shareRow("AutoMount"))
                dictShare.Add("ConnectedOnly", shareRow("ConnectedOnly"))
            End If

            arrShares.Add(dictShare)
        Next

        Dim plistNoMADShares As New NSDictionary From {
            {"Version", "1"},
            {"HomeMount", dictHomeMount},
            {"Shares", arrShares}
        }

        Dim SaveFileDialogNoMAD As New SaveFileDialog With {
            .Filter = "PLIST Files (*.plist)|*.plist|All Files (*.*)|*",
            .OverwritePrompt = True
        }

        If SaveFileDialogNoMAD.ShowDialog = DialogResult.OK Then
            PropertyListParser.SaveAsXml(plistNoMADShares, New FileInfo(SaveFileDialogNoMAD.FileName))
        End If
    End Sub

    Private Sub CheckBoxUseNoMADDefaults_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxUseNoMADDefaults.CheckedChanged
        Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
        If rows.Count > 0 Then
            rows(0)("UseNoMADDefaults") = CheckBoxUseNoMADDefaults.Checked
            ChangeMade(rows(0))
        End If
        NoMADDefaultsEval()
    End Sub

    Private Sub ButtonAddOption_Click(sender As Object, e As EventArgs) Handles ButtonAddOption.Click
        Dim result As DialogResult = DialogAddOption.ShowDialog()
        If result = DialogResult.OK Then
            Dim strShareName As String = ListBoxShareNames.SelectedValue.ToString
            Dim strOption As String = DialogAddOption.ComboBoxOptionList.SelectedItem("Option").ToString
            Dim optionRow As DataRow
            Try
                optionRow = tableMountOptions.Select(String.Format("ShareName = '{0}' And Option = '{1}'", strShareName, strOption))(0)
            Catch
                optionRow = tableMountOptions.NewRow
                optionRow("ShareName") = strShareName
                optionRow("Option") = strOption
                tableMountOptions.Rows.Add(optionRow)
            End Try
            ChangeMade(tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))(0))
        End If
    End Sub

    Private Sub CheckBoxAutoMount_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAutoMount.CheckedChanged
        If Not CheckBoxUseNoMADDefaults.Checked Then
            Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
            If rows.Count > 0 Then
                rows(0)("AutoMount") = CheckBoxAutoMount.Checked
                ChangeMade(rows(0))
            End If
        End If
    End Sub

    Private Sub CheckBoxConnectedOnly_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxConnectedOnly.CheckedChanged
        If Not CheckBoxUseNoMADDefaults.Checked Then
            Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
            If rows.Count > 0 Then
                rows(0)("ConnectedOnly") = CheckBoxConnectedOnly.Checked
                ChangeMade(rows(0))
            End If
        End If
    End Sub

    Private Sub ButtonDeleteOption_Click(sender As Object, e As EventArgs) Handles ButtonDeleteOption.Click
        Dim strShareName As String = ListBoxShareNames.SelectedValue.ToString
        Dim strOption As String = ListBoxOptions.SelectedValue.ToString
        Dim optionRow As DataRow = tableMountOptions.Select(String.Format("ShareName = '{0}' And Option = '{1}'", strShareName, strOption))(0)
        tableMountOptions.Rows.Remove(optionRow)
        ChangeMade(tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))(0))
    End Sub

    Private Sub ButtonAddNewShare_Click(sender As Object, e As EventArgs) Handles ButtonAddNewShare.Click
        Dim boolRetry As Boolean = True
        While boolRetry
            ' Show the new share dialog
            Dim result As DialogResult = DialogNewShare.ShowDialog()
            If result = DialogResult.OK Then
                ' Only add if OK is pressed
                Dim newShareName As String = DialogNewShare.TextBoxShareName.Text
                Dim newSharePath As String = DialogNewShare.TextBoxSharePath.Text
                If newShareName.Trim.Length > 0 And newSharePath.Trim.Length > 0 Then
                    ' Only add if Share Name and Path are populated with non-blank strings
                    If tableNetworkLocations.Select(String.Format("ShareName = '{0}'", newShareName)).Count > 0 Then
                        If Not MsgBox("A share with this name already exists." + vbCrLf + "Would you like to try again?", MessageBoxButtons.YesNoCancel) = DialogResult.Yes Then
                            ' No pressed, don't show the dialog again
                            boolRetry = False
                        End If
                    Else
                        ' Add the row with new UIDs
                        Dim row As DataRow = tableNetworkLocations.NewRow
                        row("ShareName") = newShareName
                        row("ShareTarget") = newSharePath
                        row("Ini1UID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
                        row("Ini2UID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
                        row("FoldersUID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
                        row("ShortcutsUID") = "{" + Guid.NewGuid.ToString.ToUpper + "}"
                        tableNetworkLocations.Rows.Add(row)
                        ListBoxShareNames.Update()
                        ListBoxShareNames.SelectedValue = newShareName
                        ChangeMade(row)
                        RefreshView()
                        ' Row added, don't show the dialog again
                        boolRetry = False
                    End If
                Else
                    If Not MsgBox("Both Share Name and Share Path are required." + vbCrLf + "Would you like to try again?", MessageBoxButtons.YesNoCancel) = DialogResult.Yes Then
                        ' No pressed, don't show the dialog again
                        boolRetry = False
                    End If
                End If
            Else
                ' Cancel pressed, don't show the dialog again
                boolRetry = False
            End If
        End While
        ' Clear New Share Dialog's text boxes
        DialogNewShare.TextBoxShareName.Clear()
        DialogNewShare.TextBoxSharePath.Clear()
    End Sub

    Private Sub ButtonDeleteShare_Click(sender As Object, e As EventArgs) Handles ButtonDeleteShare.Click
        Dim rows() As DataRow = tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
        If rows.Count > 0 Then
            ' You can only delete a row that exists
            If MsgBox("Are you sure you want to delete the share " + ListBoxShareNames.SelectedValue.ToString + "?" + vbCrLf + "This action can not be undone.", MessageBoxButtons.YesNoCancel) = DialogResult.Yes Then
                ' Yes pressed, apparently they want to delete this row.
                Dim groupRows() As DataRow = tableFilterGroups.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))
                If groupRows.Count > 0 Then
                    For Each row As DataRow In groupRows
                        ' Also delete all rows in the filters that have this share name
                        tableFilterGroups.Rows.Remove(row)
                    Next
                End If
                ' Actually delete the row
                tableNetworkLocations.Rows.Remove(rows(0))

                changesSaved = False

                ' Clear form data
                TextBoxLastModified.Clear()
                TextBoxLocationName.Clear()
                TextBoxTargetPath.Clear()

                ' reload form data
                RefreshView()
            End If
        End If

    End Sub

    Private Sub ButtonAddGroup_Click(sender As Object, e As EventArgs) Handles ButtonAddGroup.Click
        If ADPicker.ShowDialog = DialogResult.OK Then
            For Each SelectedObject In ADPicker.SelectedObjects
                Dim objGroup As DirectoryEntry = New DirectoryEntry(SelectedObject.Path)
                Dim strNetbiosSearchString As String = "LDAP://CN=Partitions,CN=Configuration"
                For Each part In objGroup.Properties.Item("distinguishedName").Value.ToString.Split(",")
                    If part.StartsWith("DC=") Then
                        strNetbiosSearchString += "," + part.ToString()
                    End If
                Next

                Dim strShareName As String = ListBoxShareNames.SelectedValue.ToString
                Dim strGroupName As String = GetNetBiosName(strNetbiosSearchString) + "\" + objGroup.Properties.Item("cn").Value
                Dim strGroupSID As String = (New Security.Principal.SecurityIdentifier(objGroup.Properties.Item("objectSid").Value, 0)).Value
                Dim groupRow As DataRow
                ' Add the row if it doesn't already exist
                Try
                    groupRow = tableFilterGroups.Select(String.Format("ShareName = '{0}' And GroupName = '{1}' And GroupSID = '{2}'", strShareName, strGroupName, strGroupSID))(0)
                Catch
                    groupRow = tableFilterGroups.NewRow
                    groupRow("ShareName") = strShareName
                    groupRow("GroupName") = strGroupName
                    groupRow("GroupSID") = strGroupSID
                    tableFilterGroups.Rows.Add(groupRow)
                    ChangeMade(tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))(0))
                End Try
            Next
        End If
    End Sub

    Private Function GetNetBiosName(ldapUrl As String) As String
        Dim netbiosName As String = ""
        Dim dirEntry As DirectoryEntry = New DirectoryEntry(ldapUrl)

        Dim searcher As DirectorySearcher = New DirectorySearcher(dirEntry) With {
            .Filter = "netbiosname=*"
        }
        searcher.PropertiesToLoad.Add("cn")

        Dim results As SearchResultCollection = searcher.FindAll()
        If (results.Count > 0) Then
            Dim rpvc As ResultPropertyValueCollection = results(0).Properties("CN")
            netbiosName = rpvc(0).ToString()
        End If

        Return netbiosName
    End Function

    Private Sub GenerateXMLToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles GenerateXMLToolStripMenuItem.Click
        ' Populate xml strings
        MakeXMLStrings()

        ' Populate xml objects from strings
        XMLIniFiles.LoadXml(strIniFilesXML)
        XMLFolders.LoadXml(strFoldersXML)
        XMLShortcuts.LoadXml(strShortcutsXML)

        ' Create folder if it doesn't already exist (the group policy will likely not work if this is the case)
        If Not Directory.Exists(strGPPath + "\IniFiles") Then Directory.CreateDirectory(strGPPath + "\IniFiles")
        If Not Directory.Exists(strGPPath + "\Folders") Then Directory.CreateDirectory(strGPPath + "\Folders")
        If Not Directory.Exists(strGPPath + "\Shortcuts") Then Directory.CreateDirectory(strGPPath + "\Shortcuts")

        ' Create the file if it does not exist (the group policy will likely not work if this is the case)
        If Not File.Exists(strGPPath + "\IniFiles\IniFiles.xml") Then File.Create(strGPPath + "\IniFiles\IniFiles.xml").Dispose()
        If Not File.Exists(strGPPath + "\Folders\Folders.xml") Then File.Create(strGPPath + "\Folders\Folders.xml").Dispose()
        If Not File.Exists(strGPPath + "\Shortcuts\Shortcuts.xml") Then File.Create(strGPPath + "\Shortcuts\Shortcuts.xml").Dispose()

        ' Save the xml objects to the files
        XMLIniFiles.Save(strGPPath + "\IniFiles\IniFiles.xml")
        XMLFolders.Save(strGPPath + "\Folders\Folders.xml")
        XMLShortcuts.Save(strGPPath + "\Shortcuts\Shortcuts.xml")

        ' Show saved message
        MsgBox("Changes saved to Group Policy.")
        changesSaved = True
    End Sub

    Private Sub ReadFromGroupPolicyToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ReadFromGroupPolicyToolStripMenuItem.Click
        ' TODO: Prompt about saving changes if necessary

        ' Reload data from policy xml files
        ReloadData()
        changesSaved = True
    End Sub

    Private Sub SettingsToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SettingsToolStripMenuItem.Click
        LaunchSettings()
    End Sub

    Private Sub ButtonDeleteGroup_Click(sender As Object, e As EventArgs) Handles ButtonDeleteGroup.Click
        Dim strShareName As String = DataGridViewGroups.SelectedRows(0).Cells("ShareName").Value
        Dim strGroupName As String = DataGridViewGroups.SelectedRows(0).Cells("GroupName").Value
        Dim strGroupSID As String = DataGridViewGroups.SelectedRows(0).Cells("GroupSID").Value
        Dim groupRow As DataRow = tableFilterGroups.Select(String.Format("ShareName = '{0}' And GroupName = '{1}' And GroupSID = '{2}'", strShareName, strGroupName, strGroupSID))(0)
        ' Remove group from table
        tableFilterGroups.Rows.Remove(groupRow)
        ' Update share modified time
        ChangeMade(tableNetworkLocations.Select(String.Format("ShareName = '{0}'", ListBoxShareNames.SelectedValue.ToString))(0))

    End Sub

    Private Sub AboutToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click
        AboutBox.ShowDialog()
    End Sub
End Class
