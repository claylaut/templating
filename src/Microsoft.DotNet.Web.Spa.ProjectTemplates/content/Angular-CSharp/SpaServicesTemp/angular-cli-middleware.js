var childProcess = require('child_process');

module.exports = {
    startAngularCliServer: function startAngularCliServer(callback, options) {
        var deployUrl = '/dist/'; // Should come from .angular-cli.json, but https://github.com/angular/angular-cli/issues/7347

        executeAngularCli([
            'serve',
            '--deploy-url', deployUrl,
            '--extract-css'
        ]);

        // TODO: Make server-side rendering optional via flag in Startup.cs
        // TODO: Make sure we kill the child process when this parent process is killed
        childProcess.fork(require.resolve('@angular/cli/bin/ng'), [
            'build',
            '-app', 'ssr',
            '--watch',
            '--output-path', 'dist-server'
        ]);

        // TODO: Don't issue callback until both ng processes have started, and
        // the SSR one has written its output file to disk
        callback(null, {
            Port: 4200, // TODO: Determine dynamically (and preferably avoid clashes)
            PublicPaths: [deployUrl] // TODO: Determine dynamically
        });
    }
};

function executeAngularCli(args) {
    var angularCliBin = require.resolve('@angular/cli/bin/ng');
    process.argv = process.argv.slice(0, 1);
    process.argv.push(angularCliBin);
    Array.prototype.push.apply(process.argv, args);
    require(angularCliBin);
}
