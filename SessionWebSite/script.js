const baseAddress = 'http://localhost:7071';
var app = new Vue({
    el: '#app',
    data: {
        sessions: [
        ],
        newSession: "",
        error: undefined
    },
    methods: {
        createSession: function() {
            fetch(`${baseAddress}/api/session`, { method: "POST", body: JSON.stringify({ description: this.newSession} )})
                .then(response => response.json())
                .then(json => {
                    this.sessions.push(json);
                    this.newSession = '';
                })
                .catch(reason => this.error = `Failed to create session: ${reason}`);
        },
        deleteSession: function(session) {
            var sessions = this.sessions;
            fetch(`${baseAddress}/api/session/${session.id}`, { method: "DELETE"})
                .then(function() {
                    var index = sessions.indexOf(session);
                    if (index > -1) {
                        sessions.splice(index, 1);
                    }
                })
                .catch(reason => this.error = `Failed to delete session: ${reason}`);
        },
        updateSession: function(session) {
            const body = JSON.stringify({ isAccepted: session.isAccepted });
            fetch(`${baseAddress}/api/session/${session.id}`, 
                { method: "PUT", body: body})
                .catch(reason => this.error = `Failed to update session: ${reason}`);
        },     
    },
    mounted: function () {
        fetch(`${baseAddress}/api/session`)
            .then(response => response.json())
            .then(json => this.sessions = json)
            .catch(reason => this.error = `Failed to fetch sessions: ${reason}`);
    },
});