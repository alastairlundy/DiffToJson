/*
    Copyright 2026 Alastair Lundy

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

using Microsoft.Extensions.Compliance.Redaction;
using DiffToJsonLib.Models;
using DiffToJsonLib.Redactors;

namespace DiffToJsonLib.Training;

public sealed class RedactionPolicy
{
    private readonly IReadOnlyDictionary<RedactionTier, Redactor> _redactors;

    public RedactionPolicy(IReadOnlyDictionary<RedactionTier, Redactor> redactors)
    {
        _redactors = redactors;
    }

    public CommitRecord Redact(CommitRecord commit, RedactionTier tier)
    {
        if (tier == RedactionTier.None || !_redactors.TryGetValue(tier, out var redactor))
        {
            return commit;
        }

        return tier switch
        {
            RedactionTier.Message => commit with { CommitMessage = redactor.Redact(commit.CommitMessage) },
            RedactionTier.Diff => commit with { Diff = redactor.Redact(commit.Diff) },
            RedactionTier.All => commit with
            {
                CommitMessage = redactor.Redact(commit.CommitMessage),
                Diff = redactor.Redact(commit.Diff)
            },
            _ => commit
        };
    }

    public string Redact(string text, RedactionTier tier)
    {
        if (tier == RedactionTier.None || !_redactors.TryGetValue(tier, out var redactor))
        {
            return text;
        }

        return redactor.Redact(text);
    }
}
