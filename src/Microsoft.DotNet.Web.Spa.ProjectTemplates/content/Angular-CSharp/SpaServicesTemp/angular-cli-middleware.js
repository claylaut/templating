var childProcess = require('child_process');
var net = require('net');
var readline = require('readline');
var url = require('url');

module.exports = {
    startAngularCliServer: function startAngularCliServer(callback, options) {
        // Start an @angular/cli instance to watch and write SSR
        // builds to the /dist-server dir
        // TODO: Make server-side rendering optional via flag in Startup.cs
        // TODO: Make asp-prerender-module wait until the files exist on disk
        executeAngularCli([
            'build',
            '-app', 'ssr',
            '--watch',
            '--output-path', 'dist-server'
        ]);

        getOSAssignedPortNumber().then(function (portNumber) {
            // Start @angular/cli dev server on private port, and pipe its output
            // back to the ASP.NET host process.
            // TODO: Support streaming arbitrary chunks to host process's stdout
            // rather than just full lines, so we can see progress being logged
            var devServerProc = executeAngularCli([
                'serve',
                '--port', portNumber.toString(),
                '--deploy-url', '/dist/', // Value should come from .angular-cli.json, but https://github.com/angular/angular-cli/issues/7347
                '--extract-css'
            ]);
            devServerProc.stdout.pipe(process.stdout);

            // Wait until the CLI dev server is listening before letting ASP.NET start the app
            console.log('Waiting for @angular/cli service to start...');
            var readySignal = /open your browser on (http\S+)/;
            var lineReader = readline
                .createInterface({ input: devServerProc.stdout })
                .on('line', function (line) {
                    var matches = readySignal.exec(line);
                    if (matches) {
                        var devServerUrl = url.parse(matches[1]);
                        console.log('@angular/cli service has started on internal port ' + devServerUrl.port);
                        callback(null, {
                            Port: parseInt(devServerUrl.port),
                            PublicPaths: [devServerUrl.path]
                        });
                        lineReader.close();
                    }
                });
        });
    }
};

function executeAngularCli(args) {
    var angularCliBin = require.resolve('@angular/cli/bin/ng');
    return childProcess.fork(angularCliBin, args, {
        stdio: [/* stdin */ 'ignore', /* stdout */ 'pipe', /* stderr */ 'inherit', 'ipc']
    });
}

function getOSAssignedPortNumber() {
    return new Promise(function (resolve, reject) {
        var server = net.createServer();
        server.listen(0, 'localhost', function () {
            var portNumber = server.address().port;
            server.close(function () { resolve(portNumber); });
        });
    });
}
