Imports DevExpress.Data
Imports DevExpress.XtraEditors

Namespace WinForms.Client
    Partial Public Class MainForm
        Inherits XtraForm
        Public Sub New()
            InitializeComponent()
        End Sub
        Private loader As VirtualServerModeDataLoader
        Private Sub VirtualServerModeSource_ConfigurationChanged(sender As Object, e As VirtualServerModeRowsEventArgs) Handles virtualServerModeSource.ConfigurationChanged
            loader = New VirtualServerModeDataLoader(e.ConfigurationInfo)
            e.RowsTask = loader.GetRowsAsync(e)
        End Sub
        Private Sub VirtualServerModeSource_MoreRows(sender As Object, e As VirtualServerModeRowsEventArgs) Handles virtualServerModeSource.MoreRows
            If loader IsNot Nothing Then
                e.RowsTask = loader.GetRowsAsync(e)
            End If
        End Sub
    End Class
End Namespace
