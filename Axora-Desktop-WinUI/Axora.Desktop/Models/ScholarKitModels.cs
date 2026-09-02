using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

/// <summary>
/// Model representing a chat turn in the Offline Document RAG Assistant stream.
/// </summary>
public sealed partial class ScholarChatMessage : ObservableObject
{
    public bool IsUser { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double Confidence { get; set; }
    public IReadOnlyList<string> CitedPassages { get; set; } = [];

    [ObservableProperty]
    private bool _isSpeaking;

    public bool HasCitations => CitedPassages.Count > 0;
    public string FormattedConfidence => $"{Confidence * 100:F0}% Match";
    public string FormattedTime => Timestamp.ToString("t");
}

/// <summary>
/// Model representing an extracted key concept or definition.
/// </summary>
public sealed class StudyConceptItem
{
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Category { get; set; } = "Core Concept";
    public string BadgeColor { get; set; } = "#5B7DE8";
}

/// <summary>
/// Model representing an auto-generated study / practice question.
/// </summary>
public sealed partial class StudyQuestionItem : ObservableObject
{
    public int Number { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";

    [ObservableProperty]
    private bool _isAnswerVisible;
}
