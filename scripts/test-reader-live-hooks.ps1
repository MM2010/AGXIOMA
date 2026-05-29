param(
    [switch]$OnlyWebsite,
    [switch]$OnlyWikipedia,
    [switch]$OnlyPubMed,
    [switch]$OnlyFirecrawl,
    [switch]$OnlyYouTube,
    [switch]$OnlyCloudReaders,
    [switch]$OnlyWhisper,
    [switch]$OnlyNotion,
    [switch]$OnlyConfluence
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

$allFilters = @(
    'FullyQualifiedName~WebsiteReaderTests',
    'FullyQualifiedName~WikipediaReaderTests',
    'FullyQualifiedName~PubMedReaderTests',
    'FullyQualifiedName~YouTubeTranscriptReaderTests',
    'FullyQualifiedName~FirecrawlReaderTests',
    'FullyQualifiedName~AudioWhisperReaderTests',
    'FullyQualifiedName~AWSS3ReaderTests',
    'FullyQualifiedName~AzureBlobReaderTests',
    'FullyQualifiedName~GCSReaderTests',
    'FullyQualifiedName~GitHubReaderTests',
    'FullyQualifiedName~SharePointReaderTests',
    'FullyQualifiedName~NotionReaderTests',
    'FullyQualifiedName~ConfluenceReaderTests'
)

$websiteFilters = @('FullyQualifiedName~WebsiteReaderTests')
$wikipediaFilters = @('FullyQualifiedName~WikipediaReaderTests')
$pubMedFilters = @('FullyQualifiedName~PubMedReaderTests')
$youtubeFilters = @('FullyQualifiedName~YouTubeTranscriptReaderTests')
$firecrawlFilters = @('FullyQualifiedName~FirecrawlReaderTests')
$whisperFilters = @('FullyQualifiedName~AudioWhisperReaderTests')
$notionFilters = @('FullyQualifiedName~NotionReaderTests')
$confluenceFilters = @('FullyQualifiedName~ConfluenceReaderTests')
$cloudReaderFilters = @(
    'FullyQualifiedName~AWSS3ReaderTests',
    'FullyQualifiedName~AzureBlobReaderTests',
    'FullyQualifiedName~GCSReaderTests',
    'FullyQualifiedName~GitHubReaderTests',
    'FullyQualifiedName~SharePointReaderTests'
)

$filters = if ($OnlyWebsite) {
    $websiteFilters
} elseif ($OnlyWikipedia) {
    $wikipediaFilters
} elseif ($OnlyPubMed) {
    $pubMedFilters
} elseif ($OnlyYouTube) {
    $youtubeFilters
} elseif ($OnlyFirecrawl) {
    $firecrawlFilters
} elseif ($OnlyWhisper) {
    $whisperFilters
} elseif ($OnlyNotion) {
    $notionFilters
} elseif ($OnlyConfluence) {
    $confluenceFilters
} elseif ($OnlyCloudReaders) {
    $cloudReaderFilters
} else {
    $allFilters
}

$filterArg = ($filters -join '|')

$requiredEnv = @{
    'AGXIOME_WEBSITE_TEST_URL' = 'Website reader live content URL';
    'AGXIOME_WEBSITE_TEST_404_URL' = 'Website reader live 404 URL';
    'AGXIOME_WEBSITE_TEST_REDIRECT_URL' = 'Website reader live redirect URL';
    'AGXIOME_WIKIPEDIA_TEST_TITLE' = 'Wikipedia reader live article title';
    'AGXIOME_WIKIPEDIA_TEST_URI' = 'Wikipedia reader live wikipedia:// URI';
    'AGXIOME_WIKIPEDIA_TEST_LANG_URI' = 'Wikipedia reader live language-specific URI';
    'AGXIOME_PUBMED_TEST_PMID' = 'PubMed reader live PMID';
    'AGXIOME_PUBMED_TEST_URI' = 'PubMed reader live pubmed:// URI';
    'AGXIOME_PUBMED_TEST_INVALID_PMID' = 'PubMed reader invalid PMID';
    'AGXIOME_YOUTUBE_TEST_VIDEO_ID' = 'YouTube reader captioned video id';
    'AGXIOME_YOUTUBE_TEST_URI' = 'YouTube reader captioned watch URL';
    'AGXIOME_YOUTUBE_TEST_NO_CAPTION_VIDEO_ID' = 'YouTube reader non-captioned video id';
    'AGXIOME_FIRECRAWL_API_KEY' = 'Firecrawl reader live API key';
    'AGXIOME_FIRECRAWL_TEST_URL' = 'Firecrawl reader target URL';
    'AGXIOME_FIRECRAWL_BASE_URL' = 'Firecrawl reader base URL override';
    'AGXIOME_WHISPER_API_KEY' = 'Whisper reader live API key';
    'AGXIOME_WHISPER_AUDIO_FILE' = 'Whisper reader live audio file path';
    'AGXIOME_WHISPER_BASE_URL' = 'Whisper reader base URL override';
    'AGXIOME_NOTION_TOKEN' = 'Notion reader live integration token';
    'AGXIOME_NOTION_PAGE_ID' = 'Notion reader live page ID';
    'AGXIOME_CONFLUENCE_TOKEN' = 'Confluence reader live API token';
    'AGXIOME_CONFLUENCE_INSTANCE' = 'Confluence reader live instance URL';
    'AGXIOME_CONFLUENCE_PAGE_URI' = 'Confluence reader live page URI';
    'AGXIOME_AWS_S3_REGION' = 'AWS S3 reader region';
    'AGXIOME_AWS_S3_TEST_URI' = 'AWS S3 reader live object URI';
    'AGXIOME_AWS_S3_MISSING_URI' = 'AWS S3 reader missing object URI';
    'AGXIOME_AZURE_BLOB_CONNECTION_STRING' = 'Azure Blob reader connection string';
    'AGXIOME_AZURE_BLOB_TEST_URI' = 'Azure Blob reader live blob URI';
    'AGXIOME_AZURE_BLOB_MISSING_URI' = 'Azure Blob reader missing blob URI';
    'GOOGLE_APPLICATION_CREDENTIALS' = 'GCS reader service account credentials path';
    'AGXIOME_GCS_TEST_URI' = 'GCS reader live object URI';
    'AGXIOME_GCS_MISSING_URI' = 'GCS reader missing object URI';
    'AGXIOME_GITHUB_TEST_URI' = 'GitHub reader live file URI';
    'AGXIOME_GITHUB_PRIVATE_URI' = 'GitHub reader private file URI';
    'AGXIOME_GITHUB_MISSING_URI' = 'GitHub reader missing file URI';
    'AGXIOME_GITHUB_TOKEN' = 'GitHub reader API token';
    'AGXIOME_SHAREPOINT_TEST_URI' = 'SharePoint reader live item URI';
    'AGXIOME_SHAREPOINT_ACCESS_TOKEN' = 'SharePoint reader Graph access token';
}

Write-Host '[AGXIOME] Reader live-hook prerequisite snapshot:' -ForegroundColor Cyan
foreach ($entry in $requiredEnv.GetEnumerator() | Sort-Object Name) {
    $present = -not [string]::IsNullOrWhiteSpace((Get-Item -Path ("Env:" + $entry.Key) -ErrorAction SilentlyContinue).Value)
    $status = if ($present) { 'set' } else { 'missing' }
    Write-Host ("  {0,-34} {1}" -f $entry.Key, $status)
}

Push-Location $repoRoot
try {
    & (Join-Path $scriptDir 'test-agxiome-tests-isolated.ps1') -Filter $filterArg
}
finally {
    Pop-Location
}