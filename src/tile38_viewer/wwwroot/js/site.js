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

var mapGeoFences;
var markers = {};
var trails = {};

let historicTrailLength = 5;
let historicTrailWeight = 5;
let markerSize = 10;

L.tileLayer(tileLayerUrl, {
//   opacity: 0.3,
  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
}).addTo(map);

$.getJSON("api/Tile38/GeoFences", function(data) {
    if(data.features.length>0){
        mapGeoFences = L.geoJson(data, {
            onEachFeature: function (feature, layer) {
                layer.bindPopup('<p>'+feature.properties.Name+'</p>');
            }
            // style: myStyle
        }).addTo(map);
    
        map.fitBounds(mapGeoFences.getBounds());
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

        setTimeout(callWithin, 3000);
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
            console.log(data);

            data.features.forEach(function(feature){
                markers[feature.properties.id] = (L.circle([feature.geometry.coordinates[1], feature.geometry.coordinates[0]]
                , {
                    // color: 'blue',
                    // fillColor: '#3f0',
                    // fillOpacity: 0.5,
                    radius: markerSize
                }
                    ).addTo(map)
                    .bindPopup('<p><em>'+feature.properties.shipname+'</em></p><p>'+feature.properties.id+'</p><p>'+feature.properties.timestamp+'</p>')
                    );
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
    var bounds = map.getBounds();
    $.getJSON("api/Tile38/SubscribeToEvents"
    + "/" + keys[0]
    + "/" + bounds.getSouth()
    + "/" + bounds.getWest()
    + "/" + bounds.getNorth()
    + "/" + bounds.getEast()
    , function(data) {

    })
    .fail(function(err) {
        console.error( "Error calling SubscribeToEvents" );
        console.error( err );
    });

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

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/movementHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.start().then(function () {
    console.log("connected");
    connection.invoke("emitGeoJSON", 'Hello');
});

connection.on("emitGeoJSON", (geoJSON) => {
    // console.log(geoJSON);
    var data = JSON.parse(geoJSON);

    var id = data.id;
    var object = data.object;

    // console.log(geoJSON);
    // console.log(mapGeoJSON);

    let newLatLng = [data.object.geometry.coordinates[1], data.object.geometry.coordinates[0]];

    if(markers[data.id]){

        let prevLatLng = [markers[data.id].getLatLng().lat, markers[data.id].getLatLng().lng];
        
        if(prevLatLng[0] == newLatLng[0] && prevLatLng[1] == newLatLng[1] ){
        // if(markers[data.id].getLatLng().lat == newLatLng[0] && markers[data.id].getLatLng().lng == newLatLng[1] ){
            // Hasn't moved - ignore.
            return;
        }

        let historicMarker = L.polyline([prevLatLng, newLatLng], 
            {
                color: "blue",
                width: historicTrailWeight
            }
        ).addTo(map)
        .bindPopup('<p><em>'+data.object.properties.shipname+'</em></p><p>'+data.object.properties.id+'</p><p>'+data.object.properties.timestamp+'</p>');


        // let historicMarker = (L.circle(newLatLng
        //     , {
        //         fillOpacity: 1,
        //         radius: markerSize
        //     }
        //     ).addTo(map)
        // );

        
        if(!trails[data.id]){
            trails[data.id] = [];
        }
        
        trails[data.id].push(historicMarker);
        let opacity = 1;
        let weight = historicTrailWeight
        // let radius = markerSize;
        for(var index = trails[data.id].length - 1;index >=0; --index){
            // trails[data.id][index].setStyle({fillOpacity:opacity, radius:radius});
            trails[data.id][index].setStyle({opacity:opacity, weight:weight});
            opacity -= 1/historicTrailLength;
            // radius -= markerSize/historicTrailLength;
            weight -= historicTrailWeight/historicTrailLength;
            if(opacity<=0){
                map.removeLayer(trails[data.id].shift());        
            }
        }

        markers[data.id].setLatLng(newLatLng);
        markers[data.id].bindPopup('<p><em>'+data.object.properties.shipname+'</em></p><p>'+data.object.properties.id+'</p><p>'+data.object.properties.timestamp+'</p>')

    } else {
        markers[data.id] = (L.circle(newLatLng
            , {
                fillOpacity: 0.5,
                radius: 10
            }
            ).addTo(map)
        );
    }

});