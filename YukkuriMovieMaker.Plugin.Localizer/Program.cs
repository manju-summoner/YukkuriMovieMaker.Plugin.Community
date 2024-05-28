

var csvFiles = Directory.GetFiles(@"..\..\..\..\YukkuriMovieMaker.Plugin.Community\", "*.csv", SearchOption.AllDirectories);
foreach (var csvFile in csvFiles)
{
    CreateLocalizerClassFile(csvFile);
}