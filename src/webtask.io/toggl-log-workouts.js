// SEE: https://jcpretorius.com/post/2018/automatically-sync-fitbit-workouts-to-toggl

// SECRETS:
// FITBIT_API_TOKEN
// TOGGL_API_TOKEN
// TOGGL_PROJECT_ID
//_____________________________________________

// using
const moment = require('moment');
const request = require('request');
const TogglClient = require('toggl-api');

//setup the webtask environment variables
module.exports = function (context, cb) {
    //setup the url
    var date = moment().format('YYYY-MM-DD');
    var fitbitApiUrl = 'https://api.fitbit.com/1/user/-/activities/list.json?afterDate=' + date + '&sort=desc&logType=manual&offset=0&limit=20';

    //toggl setup
    var toggl = new TogglClient({ apiToken: context.secrets.TOGGL_API_TOKEN });

    //do the api request
    request(
        {
            url: fitbitApiUrl,
            headers: {
                Authorization: 'Bearer ' + context.secrets.FITBIT_API_TOKEN,
            },
            rejectUnauthorized: true,
        },
        function (err, resonse) {
            if (err) {
                cb(null, { msg: 'ERROR: ' + err });
            } else {

                if (resonse.body !== null) {

                    //parse json response
                    var json = JSON.parse(resonse.body);

                    //check that we have workouts
                    if (json !== null && json.activities.length > 0) {

                        //log each of em
                        json.activities.forEach(function (workout) {

                            //{"description":"Meeting with possible clients","tags":["billed"],"duration":1200,"start":"2013-03-05T07:58:58.000Z","pid":123,"created_with":"curl"}

                            toggl.createTimeEntry(
                                {
                                    pid: context.secrets.TOGGL_PROJECT_ID,
                                    description: workout.activityName,
                                    duration: (workout.duration / 1000).toFixed(0),
                                    start: workout.startTime
                                },
                                function (togglErr, timeEntry) {
                                    // handle error
                                    if (togglErr) {
                                        cb(null, { msg: 'TOGGL ERROR: ' + togglErr });
                                    }
                                }
                            );

                        });

                        //done
                        cb(null, { msg: 'done' });

                    } else {
                        cb(null, { msg: 'No workouts to track' });
                    };
                }
            }
        }
    );
};