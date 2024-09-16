Imports DevExpress.CodeParser
Imports DevExpress.Data
Imports System.Diagnostics
Imports System.Net.Http
Imports System.Text.Json

Namespace WinForms.Client

    Public Class DataFetchResult
        Public Property Items() As List(Of OrderItem) = Nothing
        Public Property TotalCount() As Integer
    End Class

    ' This type is instantiated each time a configuration change in the virtual server
    ' mode source takes place. It receives the configuration information and handles
    ' the parts of it that are relevant to the data loading process.
    Public Class VirtualServerModeDataLoader

        Public Sub New(ByVal configurationInfo As VirtualServerModeConfigurationInfo)
            ' For instance, let's assume the backend supports sorting for just one field
            If configurationInfo.SortInfo?.Length > 0 Then
                Me.SortField = configurationInfo.SortInfo(0).SortPropertyName
                SortAscending = Not configurationInfo.SortInfo(0).IsDesc
            End If
        End Sub

        Public Property BatchSize As Integer = 40

        Public Property SortField As String = "Id"

        Public Property SortAscending As Boolean = True

        Public Async Function GetRowsAsync(e As VirtualServerModeRowsEventArgs) As Task(Of VirtualServerModeRowsTaskResult)
            Return Await Task.Run(Async Function()
                                      Debug.WriteLine($"Fetching data rows {e.CurrentRowCount} to {e.CurrentRowCount + BatchSize}, sorting by {SortField} ({If(SortAscending, "asc", "desc")})")

                                      Using client As New HttpClient()
                                          Dim baseUrl As String = System.Configuration.ConfigurationManager.AppSettings("baseUrl")
                                          Dim requestUrl As String = $"{baseUrl}/data/OrderItems?skip={e.CurrentRowCount}&take={BatchSize}&sortField={SortField}&sortAscending={SortAscending}"
                                          Dim response As HttpResponseMessage = Await client.GetAsync(requestUrl)
                                          response.EnsureSuccessStatusCode()
                                          Dim responseBody As String = Await response.Content.ReadAsStringAsync()

                                          Dim dataFetchResult As DataFetchResult = JsonSerializer.Deserialize(Of DataFetchResult)(responseBody, New JsonSerializerOptions With {
                                      .PropertyNameCaseInsensitive = True
                                  })

                                          If dataFetchResult Is Nothing Then
                                              Return New VirtualServerModeRowsTaskResult()
                                          End If

                                          Dim moreRowsAvailable As Boolean = e.CurrentRowCount + dataFetchResult.Items.Count < dataFetchResult.TotalCount
                                          Debug.WriteLine($"Returning {dataFetchResult.Items.Count} items, more rows available: {moreRowsAvailable}")
                                          Return New VirtualServerModeRowsTaskResult(dataFetchResult.Items, moreRowsAvailable)
                                      End Using
                                  End Function, e.CancellationToken)
        End Function
    End Class
End Namespace
