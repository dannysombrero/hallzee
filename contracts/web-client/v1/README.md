# Web-client conformance fixtures

Source: firmware and BathroomSync.Core at `9fbf4b5`. All data is fictional.
Identity outputs were independently computed with Python and Node Web Crypto.
C# and TypeScript consume the same fixtures. Web-only differences: strict record
validation, completed-stream cursor, no credential export, and blocking policy
transfers over 96 windows. Never regenerate expected values from the code under test.
