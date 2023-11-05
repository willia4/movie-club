# Zinfandel Movie Club

This is a website for a group of friends to use to decide which movies to watch together. 

### Running

This is basically a standard ASP.NET Core webapp. 

Once you've filled out an `appsettings.Development.json` file with the appropriate settings (easily gleaned from the missing values in `appsettings.json`), you can run this with 

```
export ASPNETCORE_ENVIRONMENT=Development; dotnet run
```

There is one caveat about the first-time run. See below. 

### First-Time Run And Bootstrap

To enable customizing Bootstrap, the entire library is stored in the `/vendor` directory. 

To build it and copy the appropriate files to `wwwroot`, you must use gulp which is part of the nodejs ecosystem. I apologize. This was the best I could do. 

```
npm install -g gulp-cli
npm install
gulp
```

The first command will install the gulp-cli as a global tool and should only need to be done once on any installation of node. 

The second command will download the packages needed to run this thing.

The third command will build and minify the CSS and Javascript as appropriate. 

This should only need to be done when the repo is first downloaded and, afterwards, when the bootstrap CSS is customized to any degree by a commit. 

This is, so far, the best I've been able to do. 