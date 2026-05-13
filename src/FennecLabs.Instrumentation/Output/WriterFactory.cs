namespace FennecLabs.Instrumentation.Output
{
    public static class WriterFactory
    {
        public static Writer CreateWriter(OutputFormat format, string output) =>
            format switch
            {
                OutputFormat.Json => new JsonWriter(output),
                OutputFormat.Fxt => new FxtWriter(output),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
    }
}
