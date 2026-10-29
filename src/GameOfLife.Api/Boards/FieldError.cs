namespace GameOfLife.Api.Boards;

/// <summary>What is wrong with one field of a request.</summary>
/// <param name="Field">The field's path in the request, such as <c>cells[2][5]</c>.</param>
/// <param name="Message">What is wrong with it, in a sentence for the client.</param>
public sealed record FieldError(string Field, string Message);
