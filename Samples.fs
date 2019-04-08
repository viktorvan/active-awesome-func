module ActiveAwesomeFunctions.Samples

[<Literal>]
let IssueSample = """
{
  "title": "Found a bug",
  "body": "I'm having a problem with this."
}
"""

[<Literal>]
let ContentResponse = """
{
  "type": "file",
  "encoding": "base64",
  "size": 5362,
  "name": "README.md",
  "path": "README.md",
  "content": "encoded content ...",
  "sha": "3d21ec53a331a6f037a91c368710b99387d012c1",
  "url": "https://api.github.com/repos/octokit/octokit.rb/contents/README.md",
  "git_url": "https://api.github.com/repos/octokit/octokit.rb/git/blobs/3d21ec53a331a6f037a91c368710b99387d012c1",
  "html_url": "https://github.com/octokit/octokit.rb/blob/master/README.md",
  "download_url": "https://raw.githubusercontent.com/octokit/octokit.rb/master/README.md",
  "_links": {
    "git": "https://api.github.com/repos/octokit/octokit.rb/git/blobs/3d21ec53a331a6f037a91c368710b99387d012c1",
    "self": "https://api.github.com/repos/octokit/octokit.rb/contents/README.md",
    "html": "https://github.com/octokit/octokit.rb/blob/master/README.md"
  }
}
"""

[<Literal>]
let UpdateContentRequest = """
{
  "message": "my commit message",
  "committer": {
    "name": "Scott Chacon",
    "email": "schacon@gmail.com"
  },
  "content": "bXkgdXBkYXRlZCBmaWxlIGNvbnRlbnRz",
  "sha": "329688480d39049927147c162b9d2deaf885005f"
}
"""
