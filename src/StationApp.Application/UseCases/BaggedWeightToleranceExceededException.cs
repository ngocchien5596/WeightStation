using System;

namespace StationApp.Application.UseCases;

public class BaggedWeightToleranceExceededException : InvalidOperationException
{
    public BaggedWeightToleranceExceededException(string message) : base(message)
    {
    }
}
