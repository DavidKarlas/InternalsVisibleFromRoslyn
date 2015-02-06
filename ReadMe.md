## [InternalsVisibleFrom("Roslyn.dll")]

[InternalsVisibleFrom("Roslyn.dll")] is tool which injects [InternalsVisibleTo("xxx")] into Roslyns dlls and creates NuGet packages.

# Why is injecting InternalsVisibleTo needed?
Every good library developer is trying to never break public API for backward compatibility(specially when your library is used by thousands Visual Studio plugins or NuGet Diagnostic Analyzers). Which means when Roslyn team decides to expose some type/method to public they must spend a lot of time to think if that type/method makes sense, if they can improve it, thread safety of API... This takes a lot of time and since VisualStudio 2015 has year in name(VS14 didn't :)) they don't have time atm to work on exposing all API needed for IDEs to work with Roslyn. So they cheat by using [InternalsVisibleTo("VisualStudio.dlls")]. Also keeping things that Plugins or Analyzers don't need and only VisualStudio uses makes a lot of sense because it gives VS and Roslyn team ability to change API for VS need without thinking of other library users. Problem is that this methods are essential for other tools/IDEs like OmniSharp or MonoDevelop. Developers of this tools are totally OK with API breakage(we don't live in GAC age anymore, we are in NuGet age).

[Roslyn team told us we should tell them what APIs we need/use so they make them pulic](https://github.com/dotnet/roslyn/issues/16) so they can prioritise opening that API. Which makes sense, but in age of IDEs where we learn about API via code completion it's super hard to know what they should open if you don't it's there, yes I can go look in Roslyn code but that takes a lot of time and also time between request and API becoming public in code can be very long. In this period reflection can be used... If I was OK with using reflection I wouldn't develop in C# but in JavaScript...

# How it works
Very simple really... Mostly it just opens roslyn dlls with Cecil and adds InternalsVisibleTo attribute and there is some logic to create NuGet packages. [See Program.cs](https://github.com/DavidKarlas/InternalsVisibleFromRoslyn/blob/master/Program.cs) which explains better :)
