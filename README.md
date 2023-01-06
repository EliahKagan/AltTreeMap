<!-- SPDX-License-Identifier: 0BSD -->

# AltTreeMap - tree map implementation with ad-hoc tests

AltTreeMap is a tree-based implementation of an ordered associative array. It
is a fairly straightforward non-self-balancing binary search tree.

An original goal of this project was to explore if Wolfram|Alpha can be used to
aid in unit and integration testing. That goal has only been partially
achieved. I don't know when, or if, I will continue with it.

Code on the *wolframalpha* branch, which is intended to be the default branch,
does use the Wolfram|Alpha API to verify correctness of some of the results in
test. Code on the *master* branch deliberately omits this, as the project has
some potentially valuable uses unrelated to Wolfram|Alpha integration.

In particular, this:

- demonstrates a table (associative array) implemented as a binary search tree.
- demonstrates calling out from C# to a native Ruby process.

The current code is written for use in LINQPad. Most code is in
`AltTreeMap.linq`. This does, unfortunately, limit use of the unmodified
program to Windows, at least as of this writing (because LINQPad has not been
ported to other operating systems). However, it should not be difficult to
extract the `AltTreeMap` class itself to a `.cs` file.

A Ruby script, `primes.rb`, is used to generate some test input data. So you
will also need a Ruby interpreter to be installed.

## License

All code in this repository is licensed under
[0BSD](https://spdx.org/licenses/0BSD). See [**`COPYING`**](COPYING). 0BSD is a
["public-domain
equivalent"](https://en.wikipedia.org/wiki/Public-domain-equivalent_license)
license.

Note that, when using the Wolfram|Alpha API, additional terms apply to your use
of that service: the [Wolfram|Alpha Terms of
Use](https://www.wolframalpha.com/termsofuse) and the [Wolfram|Alpha API Terms
of Use](https://products.wolframalpha.com/api/termsofuse).

## Using the software without the Wolfram|Alpha API

There are two ways to use the software without the Wolfram|Alpha API:

- Use the code on the *master* branch. That version of the code does not
  contain anything related to the Wolfram|Alpha API (except this readme). That
  branch does not just disable use of the API; code related to accessing it is
  entirely omitted.

- Use the code on the *wolframalpha* branch, but change
  `Configuration.EnableWolframAlpha` from `true` to `false`. That is, **change the `true` to `false`** in the code:

  ```csharp
  /// <summary>
  /// Use Wolfram|Alpha to double-check results in TestRefForEach.
  /// </summary>
  internal static bool EnableWolframAlpha => true;
  ```

## Using the software with the Wolfram|Alpha API

If you run the version of `AltTreeMap.linq` on the *wolframalpha* branch, it
looks for your Wolfram|Alpha API AppID in a file called `AppID`. (This path is
in `.gitignore`, since the AppID should not be committed to source control or
otherwise disclosed.)

## Continuing toward the original goal

The approach I would suggest, *if* one chooses to develop this further in
pursuit of the automated-testing goal described above, is:

1. Optionally, add more calls to the Wolfram|Alpha API to provide addition
   confirmation of test results. Ensure tests still pass.
2. Convert the existing ad-hoc unit tests to unit tests that use a testing
   framework. XUnit is probably the best choice of framework, if development is
   to continue in LINQPad, since LINQPad has specific support for it. Ensure
   the converted tests pass.
3. Add more tests, for better test coverage of the current functionality of the
   `AltTreeMap` class. Ensure the new tests pass.
4. Add tests that, because `AltTreeMap` is not self-balancing, will fail with a
   `StackOverflowException` or take excessively long to complete. Check that
   these new tests do not pass.
5. Make `AltTreeMap` self-balancing. Turning it into an AVL tree is suggested,
   but there are other good options, such as a red-black tree. Make sure all
   tests pass: both the tests that passed before, and the ones that did not.
6. Optionally, remove some of the Wolfram|Alpha API calls. The main reason to
   consider doing this, at least if the API was used much more heavily than
   before due to step #1, is if the cost of using the API becomes too great.
   Specifically, if test-driven development is used or anticipated for future
   use, it is likely that all tests in the project would be run a very large
   number of times per day.

But, as of this time, I have no plan to further pursue the project.
