namespace GitDiffToJsonLCli;

public record CommitRecord(
    string Diff,
    string CommitMessage,
    string RepoName,
    string License,
    string RepoUrl
);