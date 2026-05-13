using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions;

public class InvalidRepresentationStateException(Guid id, RepresentationStatus currentStatus, string operation)
    : Exception($"Cannot perform '{operation}' on representation '{id}': current status is '{currentStatus}'.")
{
    public RepresentationStatus CurrentStatus { get; } = currentStatus;
    public string Operation { get; } = operation;
}