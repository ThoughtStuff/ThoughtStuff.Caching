# Repository instructions

## Coding Style

- Wrap and align long parameter and argument lists.
- Wrap and align long call chains.
- Use C# idioms for the current version of C#.
  - When touching a file that has older idioms, update the file to use newer idioms.
  - Examples include:
    - Primary constructors
    - New extension method declarations
    - Square bracket initializer lists `[a, b, c]`
    - Implicit constructor `return new(arg)`

## Documentation and Comments

Err on the side of too many comments. Comments within methods should explain not just what is being done, but why.
Answering "why" is often more important than answering "what".

All public methods must be documented. When applicable member implementations should inherit docs from the base.

## Testing

Use one to three words in each test method name. Set an explicit `DisplayName`
on every xUnit `[Fact]` and `[Theory]` test, using a more descriptive phrase to
explain the behavior it verifies.
