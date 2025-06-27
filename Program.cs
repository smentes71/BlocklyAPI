using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

var sampleTodos = new Todo[] {
    new(1, "Walk the dog"),
    new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(4, "Clean the bathroom"),
    new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
};

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos);
todosApi.MapGet("/{id}", (int id) =>
    sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
        ? Results.Ok(todo)
        : Results.NotFound());

// Python code execution endpoint
app.MapPost("/execute", async (CodeExecutionRequest request) =>
{
    try
    {
        // Create prun.py file with the received code
        var pythonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "prun.py");
        await File.WriteAllTextAsync(pythonFilePath, request.Code, Encoding.UTF8);

        // Execute the Python file
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = "prun.py",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            return Results.Problem("Failed to start Python process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var response = new CodeExecutionResponse
        {
            Success = process.ExitCode == 0,
            Output = output,
            Error = error,
            ExitCode = process.ExitCode,
            Timestamp = DateTime.UtcNow,
            User = request.User
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        var errorResponse = new CodeExecutionResponse
        {
            Success = false,
            Error = ex.Message,
            ExitCode = -1,
            Timestamp = DateTime.UtcNow,
            User = request.User
        };

        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Code execution failed"
        );
    }
});

app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

public record CodeExecutionRequest(
    string Code,
    DateTime Timestamp,
    string User
);

public record CodeExecutionResponse(
    bool Success = false,
    string Output = "",
    string Error = "",
    int ExitCode = 0,
    DateTime Timestamp = default,
    string User = ""
);

[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(CodeExecutionRequest))]
[JsonSerializable(typeof(CodeExecutionResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}