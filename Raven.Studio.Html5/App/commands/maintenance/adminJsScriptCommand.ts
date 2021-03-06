import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import appUrl = require("common/appUrl");

class adminJsScriptCommand extends commandBase {
    private db: database = appUrl.getSystemDatabase();

    constructor(private script: string, private targetDatabase:string) {
        super();
    }

    execute(): JQueryPromise<any> {
        
        var url = "/admin/console/" + this.targetDatabase.toString();
        return this.post(url, ko.toJSON({ script: this.script }), this.db)
            .done(() => this.reportSuccess("Script executed"))
            .fail((response: JQueryXHR) => this.reportError("Script failed", response.responseText, response.statusText));

    }
}

export = adminJsScriptCommand;
