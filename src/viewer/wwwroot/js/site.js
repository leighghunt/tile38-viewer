// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// var porirua = [-41.135461, 174.839714]

var map = L.map('map');//.setView(porirua, 11);

var tileLayerOSM = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';

// Available Thundermap themes:
var theme = 'transport'; // Others: cycle, landscape, outdoors, transport-dark, spinal-map, pioneer, mobile-atlas, neighbourhood
// theme = 'cycle';
// theme = 'transport-dark';
// theme = 'outdoors';
// theme = 'neighbourhood';

var tileLayerThunderforest = 'https://{s}.tile.thunderforest.com/' + theme + '/{z}/{x}/{y}{r}.png';

var tileLayerUrl = tileLayerOSM;

var keys = [];

var mapGeoFences = {};
var markers = {};
var trails = {};

let historicTrailLength = 5;
let historicTrailWeight = 5;
let markerSize = 10;

// let refreshInterval = 10;
let useWebSocketsForMovementUpdates = false;


L.tileLayer(tileLayerUrl, {
//   opacity: 0.3,
  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
}).addTo(map);

$.getJSON("api/Tile38/GeoFences", function(data) {
    if(data.features.length>0){
        var geoFences = L.geoJson(data, {
            onEachFeature: function (feature, layer) {
                layer.bindPopup('<p>'+feature.properties.Name+'</p>');
                mapGeoFences[feature.properties.Name] = layer;
            }
            // style: myStyle
        }).addTo(map);
    
        map.fitBounds(geoFences.getBounds());
    } else{
        console.warn("Geofences collection is empty.");
        alert("Geofences collection is empty. You may need to refresh this page after inserting data into Tile38");
    }
})
.done(function() {
    console.log( "Added geofences to map" );
  })
.fail(function(err) {
    console.error( "Error getting GeoFences" );
    console.error( err );
})

$.getJSON("api/Tile38/KEYS/*", function(data) {
    if(data.length>0){
        keys = data;

        setTimeout(function() {callWithin(false)}, 3000);
    } else{
        console.warn("Keys collection is empty.");
        alert("Keys collection is empty. You may need to refresh this page after inserting data into Tile38");
    }
})
.done(function() {
    console.log( "Retrieved Keys" );
  })
.fail(function(err) {
    console.error( "Error getting keys" );
    console.error( err );
})

$.getJSON("Home/Settings", function(settings) {
    if(settings!=null){
        console.log(settings);
        // refreshInterval = settings.refreshInterval;
        setInterval(function() {callWithin(false)}, settings.refreshInterval * 1000);


        const connection = new signalR.HubConnectionBuilder()
        .withUrl("/movementHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

        useWebSocketsForMovementUpdates = settings.useWebSocketsForMovementUpdates;

        connection.start().then(function () {
            // console.log("connected");
            // connection.invoke("emitGeoJSON", 'Hello');
        });

        connection.on("emitGeoFence", (geoFence) => {
            var data = JSON.parse(geoFence);

            console.log(data.id + " " + data.detect + " " + data.hook);

            var geoFence = mapGeoFences[data.hook];
            // var geoFence = mapGeoFences.find(function(element){
            //     return element.properties.Name == data.hook;
            // });

            if(geoFence != null){
                console.log(geoFence);
                if(data.detect == "enter" || data.detect == "cross"){
                    geoFence.setStyle({color: "green"});
                } else {
                    geoFence.setStyle({color: "red"});
                }
            }
            // console.log(data);
        });
        

        if(settings.useWebSocketsForMovementUpdates){

            connection.on("emitGeoJSON", (geoJSON) => {
                var data = JSON.parse(geoJSON);

                var updatedData = {
                    id: data.id
                };

                if(data.object){

                    if(data.object.geometry){
                        updatedData.coordinates = [data.object.geometry.coordinates[1], data.object.geometry.coordinates[0]];
                    } else
                    {
                        updatedData.coordinates = [data.object.coordinates[1], data.object.coordinates[0]];
                    }

                    if(data.object.properties && data.object.properties["Name"]){
                        updatedData.name = data.object.properties["Name"]
                    } else {
                        updatedData.name = updatedData.id;
                    }
                    updatePosition(updatedData);

                }
            });

        }
    }
})
.fail(function(err) {
    console.error( "Error getting Refresh Interval" );
    console.error( err );
})
;

callWithin = function(updatedPosition){
    var bounds = map.getBounds();
    // console.log(bounds);
    $.getJSON("api/Tile38/WITHIN"
        + "/" + keys[0]
        + "/" + bounds.getSouth()
        + "/" + bounds.getWest()
        + "/" + bounds.getNorth()
        + "/" + bounds.getEast()
        + "/" + updatedPosition
        , function(data) {
        if(data.features.length>0){
            // console.log(data);

            data.features.forEach(function(feature){

                var updatedData = {
                    id:             feature.properties.id,
                    coordinates:    [feature.geometry.coordinates[1], feature.geometry.coordinates[0]],
                    name:           feature.properties.shipname
                }
                
                updatePosition(updatedData);
            });
        } else{
            console.warn("WITHIN returned empty collection.");
        }
    })
    .done(function() {
        // console.log( "Called Within" );
      })
    .fail(function(err) {
        console.error( "Error calling WITHIN" );
        console.error( err );
    })
}

map.on('moveend', function(e) {
    callWithin(true);
});

// map.locate({setView: false, maxZoom: 16});

function onLocationFound(e) {
    var radius = e.accuracy;

    // L.marker(e.latlng).addTo(map)

    L.circle(e.latlng, radius).addTo(map)
          .bindPopup("You are within " + radius + " meters from this point");

}

function onLocationError(e) {
    alert(e.message);
}

map.on('locationerror', onLocationError);
map.on('locationfound', onLocationFound);

updatePosition = function (data) {

    let newLatLng = [data.coordinates[0], data.coordinates[1]];

    if(markers[data.id]){

        let prevLatLng = [markers[data.id].getLatLng().lat, markers[data.id].getLatLng().lng];
        
        if(prevLatLng[0] == newLatLng[0] && prevLatLng[1] == newLatLng[1] ){
            return;
        }

        let historicMarker = L.polyline([prevLatLng, newLatLng], 
            {
                color: "blue",
                width: historicTrailWeight
            }
        ).addTo(map);
        
        if(!trails[data.id]){
            trails[data.id] = [];
        }
        
        trails[data.id].push(historicMarker);
        let opacity = 1;
        let weight = historicTrailWeight
        for(var index = trails[data.id].length - 1;index >=0; --index){
            trails[data.id][index].setStyle({opacity:opacity, weight:weight});
            opacity -= 1/historicTrailLength;
            weight -= historicTrailWeight/historicTrailLength;
            if(opacity<=0){
                map.removeLayer(trails[data.id].shift());        
            }
        }

        markers[data.id].setLatLng(newLatLng);
    } else {
        markers[data.id] = (L.circle(newLatLng
            , {
                fillOpacity: 0.5,
                radius: 10
            }
            ).addTo(map)
        );
    }
}