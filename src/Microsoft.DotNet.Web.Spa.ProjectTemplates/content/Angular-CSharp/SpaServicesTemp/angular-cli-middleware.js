module.exports = {
    startAngularCliServer: function startAngularCliServer(callback, options) {
        var deployUrl = '/dist'; // Should come from .angular-cli.json, but https://github.com/angular/angular-cli/issues/7347

        executeAngularCli([
            'serve',
            '--deploy-url', deployUrl
        ]);

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
