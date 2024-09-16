Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.EntityFrameworkCore
Imports System.Xml.Serialization
Imports System.Reflection
Imports System.IO
Imports System.Formats.Asn1.AsnWriter
Imports DataService
Imports DataService.DataService
Imports Microsoft.AspNetCore.Http
Imports System.Linq.Dynamic.Core
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics.CodeAnalysis
Imports System.Linq
Imports Microsoft.Extensions.Configuration
Imports System.Linq.Expressions

Friend Module Program

    <STAThread>
    Sub Main(args As String())
        Dim builder As WebApplicationBuilder = WebApplication.CreateBuilder(args)

        Dim connectionString As String = builder.Configuration.GetConnectionString("ConnectionString")

        builder.Services.AddDbContext(Of DataServiceDbContext)(Sub(o)
                                                                   o.UseSqlServer(connectionString, Sub(options)
                                                                                                        options.EnableRetryOnFailure()
                                                                                                    End Sub)
                                                               End Sub)

        Dim app = builder.Build()

        ' Make sure the database exists and is current
        Using scope = app.Services.CreateScope()
            Dim dbContext = scope.ServiceProvider.GetRequiredService(Of DataServiceDbContext)()
            dbContext.Database.Migrate()
        End Using

        app.MapGet("/api/populateTestData", Async Function(dbContext As DataServiceDbContext) As Task(Of IResult)
                                                Dim _assembly = Assembly.GetExecutingAssembly()
                                                Console.WriteLine(String.Join(Environment.NewLine, _assembly.GetManifestResourceNames()))
                                                Dim resourceName = _assembly.GetManifestResourceNames().Single(Function(str) str.EndsWith("order_items.xml"))

                                                Dim serializer As New XmlSerializer(GetType(List(Of OrderItem)))

                                                Using stream = _assembly.GetManifestResourceStream(resourceName)
                                                    If stream IsNot Nothing Then
                                                        Dim items = CType(serializer.Deserialize(stream), List(Of OrderItem))

                                                        If items IsNot Nothing Then
                                                            dbContext.OrderItems.AddRange(items)
                                                            Await dbContext.SaveChangesAsync()
                                                            Return Results.Ok("Data populated successfully")
                                                        End If
                                                    End If

                                                    Return Results.NotFound("Error populating data")
                                                End Using
                                            End Function)

        app.MapGet("/data/OrderItems", Async Function(dbContext As DataServiceDbContext, skip As Integer, take As Integer, sortField As String, sortAscending As Boolean) As Task(Of IResult)

                                           Dim source = OrderByDynamic(dbContext.OrderItems.AsQueryable(), sortField, sortAscending)


                                           ' Fetch the items and the total count asynchronously
                                           Dim items = Await source.Skip(skip).Take(take).ToListAsync()
                                           Dim totalCount = Await dbContext.OrderItems.CountAsync()

                                           ' Return the result as a JSON object
                                           Return Results.Ok(New With {
        Key .Items = items,
        Key .TotalCount = totalCount
    })
                                       End Function)



        app.Run()
    End Sub

    Public Function OrderByDynamic(Of TSource)(query As IQueryable(Of TSource), sortField As String, sortAscending As Boolean) As IQueryable(Of TSource)
        ' Get the type of TSource
        Dim type As Type = GetType(TSource)

        ' Create a parameter expression for the lambda
        Dim parameter = Expression.Parameter(type, "x")

        ' Create a member expression for the field to sort by
        Dim _Property = Expression.Property(parameter, sortField)

        ' Create a lambda expression for the field
        Dim lambda = Expression.Lambda(Of Func(Of TSource, Object))(Expression.Convert(_Property, GetType(Object)), parameter)

        ' Create the method call expression for OrderBy or OrderByDescending
        Dim methodName = If(sortAscending, "OrderBy", "OrderByDescending")
        Dim method = GetType(Queryable).GetMethods().
            First(Function(m) m.Name = methodName AndAlso m.GetParameters().Length = 2).
            MakeGenericMethod(type, GetType(Object))

        ' Apply the method
        Return CType(method.Invoke(Nothing, New Object() {query, lambda}), IQueryable(Of TSource))
    End Function
End Module
