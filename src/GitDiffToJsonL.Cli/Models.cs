namespace GitDiffToJsonL.Cli;

public record CommitRecord(
    string Diff,
    string CommitMessage,
    string RepoName,
    string License,
    string RepoUrl
);