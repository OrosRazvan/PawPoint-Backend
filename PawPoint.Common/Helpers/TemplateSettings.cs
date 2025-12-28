namespace PawPoint.Common.Helpers
{
    public sealed class TemplateSettings
    {
        public required string Root { get; set; } 

        public required EmailTemplates Email { get; set; }

        public sealed class EmailTemplates
        {
            public required string VerifyHtml { get; set; }
            public required string VerifyText { get; set; }
            public required string ResetHtml { get; set; }
            public required string ResetText { get; set; }
        }
    }
}
