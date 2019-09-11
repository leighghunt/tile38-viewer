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

L.tileLayer(tileLayerUrl, {
//   opacity: 0.3,
  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
}).addTo(map);

$.getJSON("api/Tile38/GeoFences", function(data) {
    if(data.features.length>0){
        var geoJSON = L.geoJson(data, {
            onEachFeature: function (feature, layer) {
                layer.bindPopup('<p>'+feature.properties.Name+'</p>');
            }
            // style: myStyle
        }).addTo(map);
    
        map.fitBounds(geoJSON.getBounds());
    } else{
        console.warn("Geofences is empty collection.");
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

        // setInterval(callWithin, 1000);
        setTimeout(callWithin, 1000);
    } else{
        console.warn("Keys is empty collection.");
    }
})
.done(function() {
    console.log( "Retrieved Keys" );
  })
.fail(function(err) {
    console.error( "Error getting keys" );
    console.error( err );
})

callWithin = function(){
    var bounds = map.getBounds();
    console.log(bounds);
    $.getJSON("api/Tile38/WITHIN"
        + "/" + keys[0]
        + "/" + bounds.getSouth()
        + "/" + bounds.getWest()
        + "/" + bounds.getNorth()
        + "/" + bounds.getEast()
        , function(data) {
        if(data.features.length>0){
            // console.log(data);

            var geoJSON = L.geoJson(data, {
            }).addTo(map);
    
    
        } else{
            console.warn("Keys is empty collection.");
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

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/movementHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.start().then(function () {
    console.log("connected");
    connection.invoke("emitGeoJSON", 'Hello');
});

connection.on("emitGeoJSON", (geoJSON) => {
    console.log(geoJSON);
});