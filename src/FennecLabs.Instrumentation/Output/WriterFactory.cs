namespace FennecLabs.Instrumentation.Output
{
    public static class WriterFactory
    {
        public static Writer CreateWriter(string writerType, string output)
        {
            if (string.Equals(writerType, "json", StringComparison.OrdinalIgnoreCase))
                return new JsonWriter(output);

            if (string.Equals(writerType, "fxt", StringComparison.OrdinalIgnoreCase))
                return new FxtWriter(output);

            throw new ArgumentException(
                $"Unknown output format: '{writerType}'. Supported formats: fxt, json.",
                nameof(writerType));
        }
    }
}

