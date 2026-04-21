using PawPoint.Services.Parsing;

namespace PawPoint.Services.Interfaces;

public interface IIntentParser
{
    ParsedAssistantIntent Parse(string message);
}