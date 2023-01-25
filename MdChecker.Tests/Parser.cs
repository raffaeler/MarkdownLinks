namespace MdChecker.Tests;

public class Parser
{
    [Fact]
    public void FindAllLinks()
    {
        Checker checker = new(default, default);
        var documents = checker.CheckMarkdownString(string.Empty, Samples.Document1)
            .OrderBy(d => d.LineNum)
            .ThenBy(d => d.Url)
            .ToList();
        var all = Samples.ResultsOk1
            .Union(Samples.ResultsFail1)
            .OrderBy(d=> d.LineNum)
            .ThenBy(d => d.Url)
            .ToList();
        if (documents.Count != all.Count) Assert.Fail($"Different number of links");

        for(int i=0; i<documents.Count; i++)
        {
            var doc = documents[i];
            var res = all[i];
            if (doc.Url != res.Url ||
                doc.LineNum != res.LineNum ||
                doc.IsWeb != res.IsWeb)
                Assert.Fail($"Document with url {doc.Url} Line {doc.LineNum} is different");
        }
       
    }
}